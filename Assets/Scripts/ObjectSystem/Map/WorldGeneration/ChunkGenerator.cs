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

        private readonly ObjectSystem            objectSystem;
        private readonly ChunkGenerationSettings settings;

        // Readonly don't allow to use ref
        private float[,] noiseMap      = new float[mapChunkSize, mapChunkSize];
        private float[,] riverMap       = new float[mapChunkSize, mapChunkSize];
        private float[,] temperatureMap = new float[mapChunkSize, mapChunkSize];
        private float[,] moistureMap    = new float[mapChunkSize, mapChunkSize];

        // Precomputed biome centers — built once in ctor, read-only after
        private readonly float[] biomeCentersT;
        private readonly float[] biomeCentersM;
        private readonly ErosionGenerator erosionGenerator;

        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(ObjectSystem _objectSystem, ChunkGenerationSettings _settings)
        {
            objectSystem = _objectSystem;
            settings     = _settings;
            
            erosionGenerator = new ErosionGenerator(_settings);

            int biomeCount = settings.biomesSettings?.Length ?? 0;
            biomeCentersT = new float[biomeCount];
            biomeCentersM = new float[biomeCount];
            for (int i = 0; i < biomeCount; i++)
            {
                Vector2 c          = settings.biomesSettings[i].Center;
                biomeCentersT[i]  = c.x;
                biomeCentersM[i]  = c.y;
            }
        }

        public ChunkData GenerateMapData(Vector2Int centre)
        {
            FastRandom chunkRandom = new(settings.seed + centre.x + centre.y);

            Noise.GenerateHeightMap(ref noiseMap, centre);
            Noise.GenerateRiverMap(ref riverMap, centre);
            Noise.GenerateClimateMap(ref temperatureMap, ref moistureMap, centre);

            ApplyBiomeModifiers();
            erosionGenerator.ApplyErosion(centre, ref noiseMap);

            int totalCells = mapChunkSize * mapChunkSize;

            using NativeArray<float> heightNative      = new(totalCells, Allocator.TempJob);
            using NativeArray<float> temperatureNative = new(totalCells, Allocator.TempJob);
            using NativeArray<float> moistureNative    = new(totalCells, Allocator.TempJob);
            using NativeArray<int>   heightResult      = new(totalCells, Allocator.TempJob);
            using NativeArray<int>   biomeResult       = new(totalCells, Allocator.TempJob);

            Flatten(noiseMap,       heightNative);
            Flatten(temperatureMap, temperatureNative);
            Flatten(moistureMap,    moistureNative);

            NativeArray<float> regionThresholds = new(settings.regions.Length,  Allocator.TempJob);
            NativeArray<float> biomeCentersTNative    = new(biomeCentersT.Length,      Allocator.TempJob);
            NativeArray<float> biomeCentersMNative    = new(biomeCentersM.Length,      Allocator.TempJob);

            try
            {
                for (int i = 0; i < settings.regions.Length; i++)
                    regionThresholds[i] = settings.regions[i].height;

                biomeCentersTNative.CopyFrom(biomeCentersT);
                biomeCentersMNative.CopyFrom(biomeCentersM);

                new RegionAssignJob
                {
                    heightValues        = heightNative,
                    temperatureValues   = temperatureNative,
                    moistureValues      = moistureNative,
                    regionThresholds    = regionThresholds,
                    biomeCentersTNative       = biomeCentersTNative,
                    biomeCentersMNative       = biomeCentersMNative,
                    mountainRegionIndex = settings.mountainRegionIndex,
                    heightMap           = heightResult,
                    biomeMap            = biomeResult,
                }.Schedule(totalCells, 64).Complete();
            }
            finally
            {
                regionThresholds.Dispose();
                biomeCentersTNative.Dispose();
                biomeCentersMNative.Dispose();
            }

            float[,] heightRaw = (float[,])noiseMap.Clone();
            int[,]   heightMap = new int[mapChunkSize, mapChunkSize];
            int[,]   biomeMap  = new int[mapChunkSize, mapChunkSize];

            Unflatten(heightResult, heightMap);
            Unflatten(biomeResult,  biomeMap);

            SortedSet<Vector2Int> objectsToInst = new(new Vector2IntComparer());

            if (settings.endlessFlag[2])
                SpawnObjects(centre, heightMap, biomeMap, objectsToInst, chunkRandom);

            return new ChunkData(heightRaw, heightMap, biomeMap, objectsToInst);
        }
        private void ApplyBiomeModifiers()
        {
            BiomeSettings[] biomes     = settings.biomesSettings;
            int             biomeCount = biomes.Length;

            const float blendRadius     = 0.25f;
            const float rcpR2           = 1f / (blendRadius * blendRadius);
            const float altitudeCooling = 0.35f;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float baseH    = noiseMap[x, y];
                    float temp     = Mathf.Clamp01(temperatureMap[x, y] - baseH * altitudeCooling);
                    float moisture = moistureMap[x, y];

                    float totalW        = 0f;
                    float blendedScale  = 0f;
                    float blendedOffset = 0f;
                    float blendedRiverT = 0f;
                    float blendedBedH   = 0f;
                    float rv            = riverMap[x, y];

                    for (int b = 0; b < biomeCount; b++)
                    {
                        float dt = temp     - biomeCentersT[b];
                        float dm = moisture - biomeCentersM[b];
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

                    noiseMap[x, y] = h;
                }
            }
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
            int regionCount = settings.regions.Length;

            for (int x = 0; x < mapChunkSize; x++)
            {
                for (int y = 0; y < mapChunkSize; y++)
                {
                    int regionIdx = heightMap[x, y];
                    int biomeIdx  = biomeMap[x, y];

                    if ((uint)regionIdx >= (uint)regionCount) continue;

                    TerrainType region = settings.regions[regionIdx];
                    if ((uint)biomeIdx >= (uint)region.level.Length) continue;

                    TemperatureLevel level = region.level[biomeIdx];

                    for (int i = 0; i < level.objects.Length; i++)
                    {
                        ObjectInfoGeneration gen = level.objects[i];
                        if (gen.Chance == 0 || gen.info == null) continue;

                        if (chunkRandom.Range(0, settings.rareness) < gen.Chance)
                        {
                            Vector2Int posobj = new(
                                centre.x * generationSize + x * scale,
                                (centre.y - 1) * generationSize + y * scale);
                            Vector3 realPos = new(posobj.x, heightMap[x, y], posobj.y);

                            if (objectSystem.TryAddToGlobal(posobj, realPos, gen.info.PrefabID, gen.info.DefaultAmount, gen.info.InstanceType))
                            {
                                objectsToInst.Add(posobj);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            erosionGenerator.Dispose();
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        private struct RegionAssignJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> heightValues;
            [ReadOnly] public NativeArray<float> temperatureValues;
            [ReadOnly] public NativeArray<float> moistureValues;
            [ReadOnly] public NativeArray<float> regionThresholds;
            [ReadOnly] public NativeArray<float> biomeCentersTNative;
            [ReadOnly] public NativeArray<float> biomeCentersMNative;
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

                for (int b = 0; b < biomeCentersTNative.Length; b++)
                {
                    float dt = temp  - biomeCentersTNative[b];
                    float dm = moist - biomeCentersMNative[b];
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