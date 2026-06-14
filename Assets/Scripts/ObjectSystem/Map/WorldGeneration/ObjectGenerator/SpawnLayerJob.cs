using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct SpawnLayerJob : IJob
    {
        [ReadOnly] public NativeArray<ObjectSpawnConfig> configs;
        [ReadOnly] public NativeArray<float> heightMap;
        [ReadOnly] public NativeArray<float> temperatureMap;
        [ReadOnly] public NativeArray<float> moistureMap;
        [ReadOnly] public int2 chunkOrigin;
        public uint seed;
        public float chunkWorldSize;
        public int mapChunkSize;
        public int scale;

        [WriteOnly] public NativeArray<ObjectInstInfo> output;
        public NativeReference<int> outputCount;
        public NativeArray<byte> gridBuffer;

        private const float GRID_CELL_SIZE = 1f;
        private const int MAX_GRID_RES = 256;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleDensityMask(float2 localPos, in ObjectSpawnConfig cfg, out int mapIdx)
        {
            int2 cell = (int2)math.floor(localPos / scale);
            mapIdx = math.clamp(cell.y * mapChunkSize + cell.x, 0, heightMap.Length - 1);
            int idx = mapIdx;

            float4 env = new(
                heightMap[idx],
                temperatureMap[idx],
                moistureMap[idx],
                0f
            );
            
            float hFactor = RangeFactor(env.x, cfg.heightRange.x, cfg.heightRange.y);
            float tFactor = RangeFactor(env.y, cfg.tempRange.x, cfg.tempRange.y);
            float mFactor = RangeFactor(env.z, cfg.moistRange.x, cfg.moistRange.y);

            float baseProb = hFactor * tFactor * mFactor;
            if (baseProb < 0.001f)
                return 0f;

            float2 noisePos = ((float2)chunkOrigin * chunkWorldSize + localPos) * cfg.noiseScale;
            float n = noise.snoise(noisePos) * 0.5f + 0.5f;
            float nFactor = math.smoothstep(
                cfg.noiseThreshold - 0.1f,
                cfg.noiseThreshold + 0.1f,
                n
            );

            return math.lerp(baseProb, baseProb * nFactor, cfg.noiseWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float RangeFactor(float v, float min, float max)
        {
            float falloff = math.max((max - min) * 0.2f, 1e-4f);
            return math.smoothstep(min, min + falloff, v) *
                (1f - math.smoothstep(max - falloff, max, v));
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryPlace(float2 localPos, ref SpatialGrid grid, ref FastRandom rng, in ObjectSpawnConfig cfg, out ObjectInstInfo info)
        {
            info = default;
            if (!grid.Check(localPos, cfg.minDistance))
                return false;

            float prob = SampleDensityMask(localPos, cfg, out int idx);
            if (rng.GetFloat() > prob)
                return false;

            grid.Mark(localPos, cfg.minDistance);

            float2 worldPos2D = new(chunkOrigin.x * chunkWorldSize + localPos.x, chunkOrigin.y * chunkWorldSize + localPos.y);
            float h = heightMap[idx] * MapGenerator.maxTerrainHeight;


            info = new ObjectInstInfo(
                new Vector3(worldPos2D.x, h, worldPos2D.y),
                cfg.prefabID,
                1
            );
            return true;
        }

        public void Execute()
        {
            int gridRes = (int)math.min(chunkWorldSize / GRID_CELL_SIZE, MAX_GRID_RES);
            SpatialGrid grid = new()
            {
                cells = gridBuffer,
                cellSize = GRID_CELL_SIZE,
                gridSize = new int2(gridRes, gridRes)
            };

            unsafe
            {
                UnsafeUtility.MemClear(gridBuffer.GetUnsafePtr(), gridBuffer.Length);
            }

            int count = outputCount.Value;

            for (int c = 0; c < configs.Length; c++)
            {
                ObjectSpawnConfig cfg = configs[c];
                if (cfg.layer != (byte)SpawnLayer.Vegetation) continue;

                FastRandom rng = new((uint)(seed ^ (c << 16) ^ ((int)SpawnLayer.Vegetation << 24)));

                float area = chunkWorldSize * chunkWorldSize;
                int targetCount = (int)(cfg.density * area / 10000f);
                if (targetCount <= 0) continue;

                if (cfg.useClusters)
                {
                    int centersPlaced = 0;
                    int gridDiv = (int)math.max(1, math.sqrt(cfg.clusterCount));
                    float cellSize = chunkWorldSize / gridDiv;

                    for (int gy = 0; gy < gridDiv && centersPlaced < cfg.clusterCount; gy++)
                    {
                        for (int gx = 0; gx < gridDiv && centersPlaced < cfg.clusterCount; gx++)
                        {
                            float2 basePos = new float2(gx + 0.5f, gy + 0.5f) * cellSize;
                            float2 jitter = 0.8f * cellSize * new float2(rng.GetFloat() - 0.5f, rng.GetFloat() - 0.5f);
                            float2 centerPos = math.clamp(basePos + jitter, 0f, chunkWorldSize - 0.1f);

                            centersPlaced++;

                            for (int k = 0; k < cfg.clusterSize; k++)
                            {
                                float angle = rng.GetFloat() * math.PI * 2f;
                                float dist = rng.GetFloat() * cfg.clusterRadius;
                                float2 offset = new float2(math.cos(angle), math.sin(angle)) * dist;
                                float2 memberPos = math.clamp(centerPos + offset, 0f, chunkWorldSize - 0.1f);

                                if (count < output.Length && TryPlace(memberPos, ref grid, ref rng, cfg, out ObjectInstInfo inst))
                                {
                                    output[count] = inst;
                                    count++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    int gridDiv = (int)math.max(1, math.sqrt(targetCount));
                    float cellSize = chunkWorldSize / gridDiv;

                    for (int gy = 0; gy < gridDiv && count < output.Length; gy++)
                    {
                        for (int gx = 0; gx < gridDiv && count < output.Length; gx++)
                        {
                            float2 basePos = new float2(gx + 0.5f, gy + 0.5f) * cellSize;
                            float2 jitter = 0.8f * cellSize * new float2(rng.GetFloat() - 0.5f, rng.GetFloat() - 0.5f);
                            float2 pos = math.clamp(basePos + jitter, 0f, chunkWorldSize - 0.1f);

                            if (TryPlace(pos, ref grid, ref rng, cfg, out ObjectInstInfo inst))
                            {
                                output[count] = inst;
                                count++;
                            }
                        }
                    }
                }
            }

            outputCount.Value = count;
        }
    }

    public struct SpatialGrid
    {
        public NativeArray<byte> cells;
        public float cellSize;
        public int2 gridSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Check(float2 pos, float radius)
        {
            int r = (int)math.ceil(radius / cellSize);
            int2 c = (int2)math.floor(pos / cellSize);
            int2 min = math.max(c - r, 0);
            int2 max = math.min(c + r, gridSize - 1);

            for (int y = min.y; y <= max.y; y++)
            {
                int rowOffset = y * gridSize.x;
                for (int x = min.x; x <= max.x; x++)
                {
                    if (cells[rowOffset + x] != 0)
                        return false;
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Mark(float2 pos, float radius)
        {
            int r = (int)math.ceil(radius / cellSize);
            int2 c = (int2)math.floor(pos / cellSize);
            int2 min = math.max(c - r, 0);
            int2 max = math.min(c + r, gridSize - 1);

            for (int y = min.y; y <= max.y; y++)
            {
                int rowOffset = y * gridSize.x;
                for (int x = min.x; x <= max.x; x++)
                {
                    cells[rowOffset + x] = 1;
                }
            }
        }
    }
}