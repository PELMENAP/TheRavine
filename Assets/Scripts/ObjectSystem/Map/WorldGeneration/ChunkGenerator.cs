using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Events;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public sealed class ChunkGenerator : IDisposable
    {
        private const int mapChunkSize = MapGenerator.mapChunkSize, scale = MapGenerator.scale, generationSize = scale * mapChunkSize, totalCells = mapChunkSize * mapChunkSize;
        private const float maxTerrainHeight = MapGenerator.maxTerrainHeight;

        private readonly ObjectSystem objectSystem;
        private readonly ChunkGenerationSettings settings;

        private NativeArray<float> noiseMap;
        private readonly NativeArray<float> riverMap, temperatureMap, moistureMap;

        private readonly NativeArray<int> heightResult, biomeResult;

        private readonly NativeArray<float> biomeCentersT, biomeCentersM, regionThresholds;

        private readonly float[] chunkBiomeWeights;


        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(
            ObjectSystem objectSystem,
            ChunkGenerationSettings settings)
        {
            this.objectSystem = objectSystem;
            this.settings = settings;

            noiseMap = new NativeArray<float>(totalCells, Allocator.Persistent);
            riverMap = new NativeArray<float>(totalCells, Allocator.Persistent);
            temperatureMap = new NativeArray<float>(totalCells, Allocator.Persistent);
            moistureMap = new NativeArray<float>(totalCells, Allocator.Persistent);

            heightResult = new NativeArray<int>(totalCells, Allocator.Persistent);
            biomeResult = new NativeArray<int>(totalCells, Allocator.Persistent);

            int biomeCount = settings.biomesSettings?.Length ?? 0;

            biomeCentersT = new NativeArray<float>(biomeCount, Allocator.Persistent);
            biomeCentersM = new NativeArray<float>(biomeCount, Allocator.Persistent);

            chunkBiomeWeights = new float[biomeCount];

            for (int i = 0; i < biomeCount; i++)
            {
                Vector2 center = settings.biomesSettings[i].Center;

                biomeCentersT[i] = center.x;
                biomeCentersM[i] = center.y;
            }

            regionThresholds =
                new NativeArray<float>(
                    settings.regions.Length,
                    Allocator.Persistent);

            for (int i = 0; i < settings.regions.Length; i++)
            {
                regionThresholds[i] = settings.regions[i].height;
            }
        }

        public ChunkData GenerateMapData(Vector2Int centre)
        {
            int hash =
                settings.seed ^
                (centre.x * 73856093) ^
                (centre.y * 19349663);

            FastRandom chunkRandom = new(hash);

            Noise.GenerateHeightMap(noiseMap, centre);
            Noise.GenerateRiverMap(riverMap, centre);
            Noise.GenerateClimateMap(
                temperatureMap,
                moistureMap,
                centre);

            ApplyBiomeModifiers();
            ApplyErosion(centre);


            RegionAssignJob regionJob = new()
            {
                heightValues = noiseMap,
                temperatureValues = temperatureMap,
                moistureValues = moistureMap,
                regionThresholds = regionThresholds,
                biomeCentersT = biomeCentersT,
                biomeCentersM = biomeCentersM,
                mountainRegionIndex = settings.mountainRegionIndex,
                heightMap = heightResult,
                biomeMap = biomeResult
            };

            regionJob.Schedule(totalCells, 64).Complete();

            float[,] heightRaw = new float[mapChunkSize, mapChunkSize];
            int[,] heightMap = new int[mapChunkSize, mapChunkSize];
            int[,] biomeMap = new int[mapChunkSize, mapChunkSize];

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    int idx = Idx(x, y);

                    heightRaw[x, y] = noiseMap[idx] * maxTerrainHeight;
                    heightMap[x, y] = heightResult[idx];
                    biomeMap[x, y] = biomeResult[idx];
                }
            }

            SortedSet<Vector2Int> objectsToInst =
                new(new Vector2IntComparer());

            if (settings.endlessFlag[2])
            {
                SpawnObjects(
                    centre,
                    noiseMap,
                    heightResult,
                    biomeResult,
                    objectsToInst,
                    chunkRandom);
            }

            return new ChunkData(
                heightRaw,
                heightMap,
                biomeMap,
                objectsToInst);
        }

        public void ApplyErosion(Vector2Int chunk)
        {
            if (!settings.erosion.enabled)
                return;

            int seed =
                settings.seed ^
                (chunk.x * 73856093) ^
                (chunk.y * 19349663);

            HydraulicErosionJob job = new()
            {
                mapSize = MapGenerator.mapChunkSize,
                seed = seed,
                settings = settings.erosion,
                heightMap = noiseMap
            };

            job.Schedule().Complete();
        }

        private void ApplyBiomeModifiers()
        {
            BiomeSettings[] biomes = settings.biomesSettings;
            int biomeCount = biomes.Length;

            const float blendRadius = 0.25f;
            const float rcpR2 = 1f / (blendRadius * blendRadius);
            const float altitudeCooling = 0.35f;

            Array.Clear(chunkBiomeWeights, 0, biomeCount);

            float totalChunkWeight = 0f;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    int idx = Idx(x, y);

                    float baseH = noiseMap[idx];

                    float temp =
                        Mathf.Clamp01(
                            temperatureMap[idx] -
                            baseH * altitudeCooling);

                    float moisture = moistureMap[idx];

                    float totalW = 0f;
                    float blendedScale = 0f;
                    float blendedOffset = 0f;
                    float blendedRiverT = 0f;
                    float blendedBedH = 0f;

                    float rv = riverMap[idx];

                    for (int b = 0; b < biomeCount; b++)
                    {
                        float dt = temp - biomeCentersT[b];
                        float dm = moisture - biomeCentersM[b];

                        float d2 = (dt * dt + dm * dm) * rcpR2;
                        float w = math.saturate(1f - d2);
                        w = w * w * (3f - 2f * w);

                        totalW += w;

                        blendedScale +=
                            w * biomes[b].heightScale;

                        blendedOffset +=
                            w * biomes[b].heightOffset;

                        if (biomes[b].hasRivers)
                        {
                            RiverBlendSettings rs =
                                biomes[b].riverBlend;

                            float riverCenter =
                                (rs.riverMin + rs.riverMax) * 0.5f;

                            float totalRadius =
                                (rs.riverMax - rs.riverMin) * 0.5f +
                                rs.influenceWidth;

                            float dist =
                                Mathf.Abs(rv - riverCenter);

                            if (dist < totalRadius)
                            {
                                float t = 1f - dist / totalRadius;

                                t = t * t * (3f - 2f * t);

                                blendedRiverT += w * t;
                                blendedBedH += w * rs.riverBedHeight;
                            }
                        }

                        chunkBiomeWeights[b] += w;
                        totalChunkWeight += w;
                    }

                    float rcpW = 0f;
                    if (totalW > 0.0001f)
                    {
                        rcpW = 1f / totalW;
                    }
                    else
                    {
                        totalW = 1f; 
                        rcpW = 1f;
                        blendedScale = biomes[0].heightScale;
                        blendedOffset = biomes[0].heightOffset;
                    }


                    float h =
                        baseH * (blendedScale * rcpW) +
                        blendedOffset * rcpW;

                    h = Mathf.Clamp01(h);

                    float riverT = blendedRiverT * rcpW;

                    if (riverT > 0f)
                    {
                        h = Mathf.LerpUnclamped(
                            h,
                            blendedBedH * rcpW,
                            riverT);
                    }

                    noiseMap[idx] = h;
                }
            }

            if (totalChunkWeight > 0f)
            {
                float rcp = 1f / totalChunkWeight;

                for (int b = 0; b < biomeCount; b++)
                {
                    chunkBiomeWeights[b] *= rcp;
                }
            }
        }

        private void SpawnObjects(
            Vector2Int centre,
            NativeArray<float> heightRaw,
            NativeArray<int> heightMap,
            NativeArray<int> biomeMap,
            SortedSet<Vector2Int> objectsToInst,
            FastRandom chunkRandom)
        {
            int regionCount = settings.regions.Length;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    int idx = Idx(x, y);

                    float realHeight = heightRaw[idx];
                    int regionIdx = heightMap[idx];
                    int biomeIdx = biomeMap[idx];

                    if ((uint)regionIdx >= (uint)regionCount)
                        continue;

                    TerrainType region = settings.regions[regionIdx];

                    if ((uint)biomeIdx >= (uint)region.level.Length)
                        continue;

                    TemperatureLevel level = region.level[biomeIdx];

                    for (int i = 0; i < level.objects.Length; i++)
                    {
                        ObjectInfoGeneration gen = level.objects[i];

                        if (gen.Chance == 0 || gen.info == null)
                            continue;

                        if (chunkRandom.Range(0, settings.rareness) >= gen.Chance)
                            continue;

                        Vector2Int posobj = new(
                            centre.x * generationSize + x * scale,
                            (centre.y - 1) * generationSize + y * scale);

                        Vector3 realPos = new(
                            posobj.x,
                            realHeight * maxTerrainHeight,
                            posobj.y);

                        if (!objectSystem.TryAddToGlobal(
                                posobj,
                                realPos,
                                gen.info.PrefabID,
                                gen.info.DefaultAmount,
                                gen.info.InstanceType))
                            continue;

                        objectsToInst.Add(posobj);
                        break;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Idx(int x, int y)
        {
            return y * mapChunkSize + x;
        }

        public void Dispose()
        {
            if (noiseMap.IsCreated)
                noiseMap.Dispose();

            if (riverMap.IsCreated)
                riverMap.Dispose();

            if (temperatureMap.IsCreated)
                temperatureMap.Dispose();

            if (moistureMap.IsCreated)
                moistureMap.Dispose();

            if (heightResult.IsCreated)
                heightResult.Dispose();

            if (biomeResult.IsCreated)
                biomeResult.Dispose();

            if (biomeCentersT.IsCreated)
                biomeCentersT.Dispose();

            if (biomeCentersM.IsCreated)
                biomeCentersM.Dispose();

            if (regionThresholds.IsCreated)
                regionThresholds.Dispose();
        }
    }
}