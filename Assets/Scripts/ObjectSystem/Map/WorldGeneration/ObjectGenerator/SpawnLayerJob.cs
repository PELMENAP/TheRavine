using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct SpawnLayerJob : IJob
    {
        [ReadOnly] public NativeArray<ObjectSpawnConfig> configs;
        public int firstConfig;
        public int endConfig;
        [ReadOnly] public NativeArray<float> heightRaw;
        [ReadOnly] public NativeArray<float> temperatureMap;
        [ReadOnly] public NativeArray<float> moistureMap;
        [ReadOnly] public NativeArray<ObjectInstInfo> existing;
        [ReadOnly] public int2 chunkOrigin;
        public uint seed;
        [WriteOnly] public NativeArray<ObjectInstInfo> output;
        public NativeReference<int> outputCount;
        public NativeArray<byte> gridBuffer;

        private const int Size = MapGenerator.mapChunkSize;
        private const float ChunkWorld = MapGenerator.chunkSize;
        private const float InvScale = 1f / MapGenerator.scale;
        private const float InvMaxHeight = 1f / MapGenerator.maxTerrainHeight;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleDensityMask(float2 localPos, int idx, in ObjectSpawnConfig cfg)
        {
            float hFactor = RangeFactor(heightRaw[idx] * InvMaxHeight, cfg.heightRange.x, cfg.heightRange.y);
            float tFactor = RangeFactor(temperatureMap[idx], cfg.tempRange.x, cfg.tempRange.y);
            float mFactor = RangeFactor(moistureMap[idx], cfg.moistRange.x, cfg.moistRange.y);

            float baseProb = hFactor * tFactor * mFactor;
            if (baseProb < 0.001f)
                return 0f;

            float2 noisePos = ((float2)chunkOrigin * ChunkWorld + localPos) * cfg.noiseScale;
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
        private bool TryPlace(float2 localPos, ref SpatialGrid grid, ref FastRandom rng, in ObjectSpawnConfig cfg, int configIndex, out ObjectInstInfo info)
        {
            info = default;
            if (localPos.x < 0f || localPos.y < 0f || localPos.x >= ChunkWorld || localPos.y >= ChunkWorld)
                return false;

            int2 cell = math.min((int2)(localPos * InvScale), Size - 1);
            if (!grid.Check(cell, cfg.radiusCells))
                return false;

            int idx = (cell.y << MapGenerator.RowShift) | cell.x;
            float prob = SampleDensityMask(localPos, idx, cfg);
            if (rng.GetFloat() > prob)
                return false;

            grid.Mark(cell, cfg.radiusCells);

            float2 worldPos2D = (float2)chunkOrigin * ChunkWorld + localPos;

            info = new ObjectInstInfo(
                new Vector3(worldPos2D.x, heightRaw[idx], worldPos2D.y),
                cfg.prefabID,
                1
            );
            info.PrimaryIdx = idx;
            info.SpawnConfig = configIndex;
            return true;
        }

        private unsafe int RestoreGrid(ref SpatialGrid grid)
        {
            int placed = 0;
            for (int i = 0; i < existing.Length; i++)
            {
                ObjectInstInfo e = existing[i];
                if ((uint)e.PrimaryIdx >= (uint)(Size * Size)) continue;

                int2 cell = new(e.PrimaryIdx & MapGenerator.RowMask, e.PrimaryIdx >> MapGenerator.RowShift);
                if (e.SpawnConfig >= 0 && e.SpawnConfig < firstConfig)
                {
                    grid.Mark(cell, configs[e.SpawnConfig].radiusCells);
                    placed++;
                    continue;
                }

                grid.Mark(cell, 0);
                for (int s = 0; s < e.SecondaryCount; s++)
                {
                    int sec = e.SecondaryIdxs[s];
                    if ((uint)sec < (uint)(Size * Size))
                        grid.Mark(new int2(sec & MapGenerator.RowMask, sec >> MapGenerator.RowShift), 0);
                }
            }
            return placed;
        }

        public void Execute()
        {
            SpatialGrid grid = new() { cells = gridBuffer };

            unsafe
            {
                UnsafeUtility.MemClear(gridBuffer.GetUnsafePtr(), gridBuffer.Length);
            }

            int placed = RestoreGrid(ref grid);
            int capacity = output.Length;
            int written = 0;

            for (int c = firstConfig; c < endConfig; c++)
            {
                ObjectSpawnConfig cfg = configs[c];
                if (cfg.layer != (byte)SpawnLayer.Vegetation) continue;

                FastRandom rng = new((uint)(seed ^ (c << 16) ^ ((int)SpawnLayer.Vegetation << 24)));

                float area = ChunkWorld * ChunkWorld;
                int targetCount = (int)(cfg.density * area / 10000f);
                if (targetCount <= 0) continue;

                if (cfg.useClusters)
                {
                    int centersPlaced = 0;
                    int gridDiv = (int)math.max(1, math.sqrt(cfg.clusterCount));
                    float cellSize = ChunkWorld / gridDiv;

                    for (int gy = 0; gy < gridDiv && centersPlaced < cfg.clusterCount; gy++)
                    {
                        for (int gx = 0; gx < gridDiv && centersPlaced < cfg.clusterCount; gx++)
                        {
                            float2 basePos = new float2(gx + 0.5f, gy + 0.5f) * cellSize;
                            float2 jitter = 0.8f * cellSize * new float2(rng.GetFloat() - 0.5f, rng.GetFloat() - 0.5f);
                            float2 centerPos = basePos + jitter;

                            centersPlaced++;

                            for (int k = 0; k < cfg.clusterSize; k++)
                            {
                                float angle = rng.GetFloat() * math.PI * 2f;
                                float dist = rng.GetFloat() * cfg.clusterRadius;
                                float2 memberPos = centerPos + new float2(math.cos(angle), math.sin(angle)) * dist;

                                if (placed < capacity && TryPlace(memberPos, ref grid, ref rng, cfg, c, out ObjectInstInfo inst))
                                {
                                    output[written++] = inst;
                                    placed++;
                                }
                            }
                        }
                    }
                }
                else
                {
                    int gridDiv = (int)math.max(1, math.sqrt(targetCount));
                    float cellSize = ChunkWorld / gridDiv;

                    for (int gy = 0; gy < gridDiv && placed < capacity; gy++)
                    {
                        for (int gx = 0; gx < gridDiv && placed < capacity; gx++)
                        {
                            float2 basePos = new float2(gx + 0.5f, gy + 0.5f) * cellSize;
                            float2 jitter = 0.8f * cellSize * new float2(rng.GetFloat() - 0.5f, rng.GetFloat() - 0.5f);

                            if (TryPlace(basePos + jitter, ref grid, ref rng, cfg, c, out ObjectInstInfo inst))
                            {
                                output[written++] = inst;
                                placed++;
                            }
                        }
                    }
                }
            }

            outputCount.Value = written;
        }
    }

    public struct SpatialGrid
    {
        private const int Size = MapGenerator.mapChunkSize;

        public NativeArray<byte> cells;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Check(int2 c, int r)
        {
            int2 min = math.max(c - r, 0);
            int2 max = math.min(c + r, Size - 1);

            for (int y = min.y; y <= max.y; y++)
            {
                int rowOffset = y << MapGenerator.RowShift;
                for (int x = min.x; x <= max.x; x++)
                {
                    if (cells[rowOffset + x] != 0)
                        return false;
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Mark(int2 c, int r)
        {
            int2 min = math.max(c - r, 0);
            int2 max = math.min(c + r, Size - 1);

            for (int y = min.y; y <= max.y; y++)
            {
                int rowOffset = y << MapGenerator.RowShift;
                for (int x = min.x; x <= max.x; x++)
                    cells[rowOffset + x] = 1;
            }
        }
    }
}
