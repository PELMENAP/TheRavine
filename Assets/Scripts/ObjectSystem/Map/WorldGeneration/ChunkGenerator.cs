using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Events;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public class ChunkGenerator : IDisposable
    {
        private const int mapChunkSize   = MapGenerator.mapChunkSize;
        private const int scale          = MapGenerator.scale;
        private const int generationSize = scale * mapChunkSize;

        private readonly ObjectSystem            _objectSystem;
        private readonly ChunkGenerationSettings _settings;

        // Readonly don't allow to use ref
        private float[,] _noiseMap      = new float[mapChunkSize, mapChunkSize];
        private float[,] _riverMap       = new float[mapChunkSize, mapChunkSize];
        private float[,] _temperatureMap = new float[mapChunkSize, mapChunkSize];
        private float[,] _moistureMap    = new float[mapChunkSize, mapChunkSize];

        // Precomputed biome centers — built once in ctor, read-only after
        private readonly float[] _biomeCentersT;
        private readonly float[] _biomeCentersM;

        // Erosion GPU resources — lazy-init, reused across chunks
        private ComputeBuffer _erosionMapBuffer;
        private ComputeBuffer _erosionStartPosBuffer;
        private float[]       _erosionFlatMap;
        private Vector2[]     _erosionStartPos;
        private int           _cachedDropletCount = -1;
        private int           _erosionKernel      = -1;

        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(ObjectSystem objectSystem, ChunkGenerationSettings settings)
        {
            _objectSystem = objectSystem;
            _settings     = settings;

            int biomeCount = settings.biomesSettings?.Length ?? 0;
            _biomeCentersT = new float[biomeCount];
            _biomeCentersM = new float[biomeCount];
            for (int i = 0; i < biomeCount; i++)
            {
                Vector2 c          = settings.biomesSettings[i].Center;
                _biomeCentersT[i]  = c.x;
                _biomeCentersM[i]  = c.y;
            }
        }

        public ChunkData GenerateMapData(Vector2Int centre)
        {
            FastRandom chunkRandom = new(_settings.seed + centre.x + centre.y);

            Noise.GenerateHeightMap(ref _noiseMap, centre);
            Noise.GenerateRiverMap(ref _riverMap, centre);
            Noise.GenerateClimateMap(ref _temperatureMap, ref _moistureMap, centre);

            ApplyBiomeModifiers();
            ApplyErosion(centre);

            int totalCells = mapChunkSize * mapChunkSize;

            using NativeArray<float> heightNative      = new(totalCells, Allocator.TempJob);
            using NativeArray<float> temperatureNative = new(totalCells, Allocator.TempJob);
            using NativeArray<float> moistureNative    = new(totalCells, Allocator.TempJob);
            using NativeArray<int>   heightResult      = new(totalCells, Allocator.TempJob);
            using NativeArray<int>   biomeResult       = new(totalCells, Allocator.TempJob);

            Flatten(_noiseMap,       heightNative);
            Flatten(_temperatureMap, temperatureNative);
            Flatten(_moistureMap,    moistureNative);

            NativeArray<float> regionThresholds = new(_settings.regions.Length,  Allocator.TempJob);
            NativeArray<float> biomeCentersT    = new(_biomeCentersT.Length,      Allocator.TempJob);
            NativeArray<float> biomeCentersM    = new(_biomeCentersM.Length,      Allocator.TempJob);

            try
            {
                for (int i = 0; i < _settings.regions.Length; i++)
                    regionThresholds[i] = _settings.regions[i].height;

                biomeCentersT.CopyFrom(_biomeCentersT);
                biomeCentersM.CopyFrom(_biomeCentersM);

                new RegionAssignJob
                {
                    heightValues        = heightNative,
                    temperatureValues   = temperatureNative,
                    moistureValues      = moistureNative,
                    regionThresholds    = regionThresholds,
                    biomeCentersT       = biomeCentersT,
                    biomeCentersM       = biomeCentersM,
                    mountainRegionIndex = _settings.mountainRegionIndex,
                    heightMap           = heightResult,
                    biomeMap            = biomeResult,
                }.Schedule(totalCells, 64).Complete();
            }
            finally
            {
                regionThresholds.Dispose();
                biomeCentersT.Dispose();
                biomeCentersM.Dispose();
            }

            float[,] heightRaw = (float[,])_noiseMap.Clone();
            int[,]   heightMap = new int[mapChunkSize, mapChunkSize];
            int[,]   biomeMap  = new int[mapChunkSize, mapChunkSize];

            Unflatten(heightResult, heightMap);
            Unflatten(biomeResult,  biomeMap);

            SortedSet<Vector2Int> objectsToInst = new(new Vector2IntComparer());

            if (_settings.endlessFlag[2])
                SpawnObjects(centre, heightMap, biomeMap, objectsToInst, chunkRandom);

            return new ChunkData(heightRaw, heightMap, biomeMap, objectsToInst);
        }
        private void ApplyBiomeModifiers()
        {
            BiomeSettings[] biomes     = _settings.biomesSettings;
            int             biomeCount = biomes.Length;

            const float blendRadius     = 0.25f;
            const float rcpR2           = 1f / (blendRadius * blendRadius);
            const float altitudeCooling = 0.35f;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float baseH    = _noiseMap[x, y];
                    float temp     = Mathf.Clamp01(_temperatureMap[x, y] - baseH * altitudeCooling);
                    float moisture = _moistureMap[x, y];

                    float totalW        = 0f;
                    float blendedScale  = 0f;
                    float blendedOffset = 0f;
                    float blendedRiverT = 0f;
                    float blendedBedH   = 0f;
                    float rv            = _riverMap[x, y];

                    for (int b = 0; b < biomeCount; b++)
                    {
                        float dt = temp     - _biomeCentersT[b];
                        float dm = moisture - _biomeCentersM[b];
                        float w  = Mathf.Exp(-(dt * dt + dm * dm) * rcpR2);

                        totalW        += w;
                        blendedScale  += w * biomes[b].heightScale;
                        blendedOffset += w * biomes[b].heightOffset;

                        if (!biomes[b].hasRivers) continue;

                        RiverBlendSettings rs = biomes[b].riverBlend;
                        float riverCenter = (rs.riverMin + rs.riverMax) * 0.5f;
                        float totalRadius = (rs.riverMax - rs.riverMin) * 0.5f + rs.influenceWidth;
                        float dist        = Mathf.Abs(rv - riverCenter);

                        if (dist < totalRadius)
                        {
                            float t = 1f - dist / totalRadius;
                            t = t * t * (3f - 2f * t);
                            blendedRiverT += w * t;
                            blendedBedH   += w * rs.riverBedHeight;
                        }
                    }

                    float rcpW = 1f / totalW;
                    float h    = baseH * (blendedScale * rcpW) + blendedOffset * rcpW;
                    h = Mathf.Clamp01(h);

                    float riverT = blendedRiverT * rcpW;
                    if (riverT > 0f)
                        h = Mathf.LerpUnclamped(h, blendedBedH * rcpW, riverT);

                    _noiseMap[x, y] = h;
                }
            }
        }

        // ─── Hydraulic Erosion ───────────────────────────────────────────────────

        // Runs the erosion compute shader on _noiseMap.
        // Layout expected by shader: map[x * sizeY + y]  (column-major).
        //
        // Buffer lifecycle: lazily created on first call, reused across chunks.
        // Recreated automatically if dropletCount changes (e.g. in Inspector).
        //
        // Race condition note: the shader does NOT use atomic writes on `map`.
        // At ~4000 droplets on a 40x40 grid the overlap probability is low and the
        // visual result is indistinguishable from a serial implementation.
        // Use InterlockedAdd with fixed-point encoding if strict correctness is needed.
        private void ApplyErosion(Vector2Int centre)
        {
            if (!_settings.erosion.enabled || _settings.erosionShader == null) return;

            ErosionSettings e        = _settings.erosion;
            int             cellCount = mapChunkSize * mapChunkSize;
            int             drops     = e.dropletCount;

            EnsureErosionBuffers(cellCount, drops);

            // Flatten _noiseMap → column-major float[] (matches shader indexing)
            for (int x = 0; x < mapChunkSize; x++)
                for (int y = 0; y < mapChunkSize; y++)
                    _erosionFlatMap[x * mapChunkSize + y] = _noiseMap[x, y];

            // Deterministic start positions — same seed → same erosion per chunk
            // Uses prime-multiplied XOR hash so adjacent chunks don't correlate
            int rngSeed = _settings.seed
                        ^ (centre.x * 73856093)
                        ^ (centre.y * 19349663);
            FastRandom rng = new(rngSeed);

            int margin = 2;
            int lo     = margin;
            int hi     = mapChunkSize - margin;

            for (int i = 0; i < drops; i++)
                _erosionStartPos[i] = new Vector2(
                    rng.Range(lo, hi),
                    rng.Range(lo, hi));

            _erosionMapBuffer.SetData(_erosionFlatMap);
            _erosionStartPosBuffer.SetData(_erosionStartPos);

            ComputeShader cs = _settings.erosionShader;
            cs.SetInt  ("sizeX",              mapChunkSize);
            cs.SetInt  ("sizeY",              mapChunkSize);
            cs.SetInt  ("lifetime",           e.SafeLifetime);
            cs.SetInt  ("dropletCount",       drops);
            cs.SetFloat("startSpeed",         e.startSpeed);
            cs.SetFloat("accel",              e.acceleration);
            cs.SetFloat("drag",               e.drag);
            cs.SetFloat("startWater",         e.startWater);
            cs.SetFloat("sedimentCapaFactor", e.sedimentCapacityFactor);
            cs.SetFloat("depSpeed",           e.depositSpeed);
            cs.SetFloat("eroSpeed",           e.erodeSpeed);
            cs.SetFloat("gravity",            e.gravity);
            cs.SetFloat("evaporateSpeed",     e.evaporateSpeed);

            cs.SetBuffer(_erosionKernel, "map",      _erosionMapBuffer);
            cs.SetBuffer(_erosionKernel, "startPos", _erosionStartPosBuffer);

            int groups = (drops + 63) / 64;
            cs.Dispatch(_erosionKernel, groups, 1, 1);

            // Synchronous readback — acceptable since GenerateMapData is already
            // called from the main thread. Switch to AsyncGPUReadback + UniTask
            // once chunk generation is fully async.
            _erosionMapBuffer.GetData(_erosionFlatMap);

            for (int x = 0; x < mapChunkSize; x++)
                for (int y = 0; y < mapChunkSize; y++)
                    _noiseMap[x, y] = Mathf.Clamp01(_erosionFlatMap[x * mapChunkSize + y]);
        }

        private void EnsureErosionBuffers(int cellCount, int dropletCount)
        {
            if (_erosionMapBuffer == null)
            {
                _erosionMapBuffer  = new ComputeBuffer(cellCount, sizeof(float));
                _erosionFlatMap    = new float[cellCount];
                _erosionKernel     = _settings.erosionShader.FindKernel("CSMain");
            }

            if (_cachedDropletCount != dropletCount)
            {
                _erosionStartPosBuffer?.Dispose();
                _erosionStartPosBuffer = new ComputeBuffer(dropletCount, sizeof(float) * 2);
                _erosionStartPos       = new Vector2[dropletCount];
                _cachedDropletCount    = dropletCount;
            }
        }

        public void Dispose()
        {
            _erosionMapBuffer?.Dispose();
            _erosionStartPosBuffer?.Dispose();
            _erosionMapBuffer      = null;
            _erosionStartPosBuffer = null;
        }

        private static void Flatten(float[,] src, NativeArray<float> dst)
        {
            int size = mapChunkSize;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    dst[y * size + x] = src[x, y];
        }

        private static void Unflatten(NativeArray<int> src, int[,] dst)
        {
            int size = mapChunkSize;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    dst[x, y] = src[y * size + x];
        }

        private void SpawnObjects(
            Vector2Int            centre,
            int[,]                heightMap,
            int[,]                biomeMap,
            SortedSet<Vector2Int> objectsToInst,
            FastRandom            chunkRandom)
        {
            int regionCount = _settings.regions.Length;

            for (int x = 0; x < mapChunkSize; x++)
            {
                for (int y = 0; y < mapChunkSize; y++)
                {
                    int regionIdx = heightMap[x, y];
                    int biomeIdx  = biomeMap[x, y];

                    if ((uint)regionIdx >= (uint)regionCount) continue;

                    TerrainType region = _settings.regions[regionIdx];
                    if ((uint)biomeIdx >= (uint)region.level.Length) continue;

                    TemperatureLevel level = region.level[biomeIdx];

                    for (int i = 0; i < level.objects.Length; i++)
                    {
                        ObjectInfoGeneration gen = level.objects[i];
                        if (gen.Chance == 0 || gen.info == null) continue;

                        if (chunkRandom.Range(0, _settings.rareness) < gen.Chance)
                        {
                            Vector2Int posobj = new(
                                centre.x * generationSize + x * scale,
                                (centre.y - 1) * generationSize + y * scale);
                            Vector3 realPos = new(posobj.x, heightMap[x, y], posobj.y);

                            if (_objectSystem.TryAddToGlobal(posobj, realPos, gen.info.PrefabID, gen.info.DefaultAmount, gen.info.InstanceType))
                            {
                                objectsToInst.Add(posobj);
                                break;
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        private struct RegionAssignJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> heightValues;
            [ReadOnly] public NativeArray<float> temperatureValues;
            [ReadOnly] public NativeArray<float> moistureValues;
            [ReadOnly] public NativeArray<float> regionThresholds;
            [ReadOnly] public NativeArray<float> biomeCentersT;
            [ReadOnly] public NativeArray<float> biomeCentersM;
            [ReadOnly] public int mountainRegionIndex;

            [WriteOnly] public NativeArray<int> heightMap;
            [WriteOnly] public NativeArray<int> biomeMap;

            public void Execute(int index)
            {
                float h           = heightValues[index];
                int   regionIndex = 0;

                for (int i = 0; i < regionThresholds.Length; i++)
                {
                    if (h >= regionThresholds[i]) regionIndex = i;
                    else break;
                }
                heightMap[index] = regionIndex;

                if (regionIndex == mountainRegionIndex)
                {
                    biomeMap[index] = 0;
                    return;
                }

                float temp   = temperatureValues[index];
                float moist  = moistureValues[index];
                int   best   = 0;
                float bestD2 = float.MaxValue;

                for (int b = 0; b < biomeCentersT.Length; b++)
                {
                    float dt = temp  - biomeCentersT[b];
                    float dm = moist - biomeCentersM[b];
                    float d2 = dt * dt + dm * dm;
                    if (d2 >= bestD2) continue;
                    bestD2 = d2;
                    best   = b;
                }

                biomeMap[index] = best;
            }
        }
    }
}