using System;
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
        private const int mapChunkSize   = MapGenerator.mapChunkSize;
        private const int scale          = MapGenerator.scale;
        private const int generationSize = MapGenerator.generationSize;
        private const int totalCells     = mapChunkSize * mapChunkSize;
        private const float maxTerrainHeight = MapGenerator.maxTerrainHeight;

        private readonly ObjectSystem objectSystem;
        private readonly ChunkGenerationSettings settings;
        private readonly MapGenerator mapGenerator;
        private NativeArray<float> noiseMap;
        private readonly NativeArray<float> riverMap, temperatureMap, moistureMap;

        private readonly NativeArray<int>   heightResult, biomeResult;
        private readonly NativeArray<float> biomeCentersT, biomeCentersM, regionThresholds;

        private readonly float[] chunkBiomeWeights;

        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(
            ObjectSystem objectSystem,
            ChunkGenerationSettings settings,
            MapGenerator mapGenerator)
        {
            this.objectSystem = objectSystem;
            this.settings     = settings;
            this.mapGenerator = mapGenerator;

            noiseMap       = new NativeArray<float>(totalCells, Allocator.Persistent);
            riverMap       = new NativeArray<float>(totalCells, Allocator.Persistent);
            temperatureMap = new NativeArray<float>(totalCells, Allocator.Persistent);
            moistureMap    = new NativeArray<float>(totalCells, Allocator.Persistent);

            heightResult = new NativeArray<int>(totalCells, Allocator.Persistent);
            biomeResult  = new NativeArray<int>(totalCells, Allocator.Persistent);

            int biomeCount = settings.biomesSettings?.Length ?? 0;

            biomeCentersT     = new NativeArray<float>(biomeCount, Allocator.Persistent);
            biomeCentersM     = new NativeArray<float>(biomeCount, Allocator.Persistent);
            chunkBiomeWeights = new float[biomeCount];

            for (int i = 0; i < biomeCount; i++)
            {
                Vector2 center = settings.biomesSettings[i].Center;
                biomeCentersT[i] = center.x;
                biomeCentersM[i] = center.y;
            }

            regionThresholds = new NativeArray<float>(settings.regions.Length, Allocator.Persistent);
            for (int i = 0; i < settings.regions.Length; i++)
                regionThresholds[i] = settings.regions[i].height;
        }
        public ChunkData GenerateMapData(Vector2Int centre)
        {
            int hash =
                settings.seed ^
                (centre.x * 73856093) ^
                (centre.y * 19349663);

            FastRandom chunkRandom = new(hash);

            // 1. Noise passes
            Noise.GenerateHeightMap(noiseMap, centre);
            Noise.GenerateRiverMap(riverMap, centre);
            Noise.GenerateClimateMap(temperatureMap, moistureMap, centre);

            // 2. Biome modifiers + river blend
            ApplyBiomeModifiers();

            // 3. Hydraulic erosion (optional)
            ApplyErosion(centre);

            // 4. Region & biome assignment (Burst job)
            new RegionAssignJob
            {
                heightValues       = noiseMap,
                temperatureValues  = temperatureMap,
                moistureValues     = moistureMap,
                regionThresholds   = regionThresholds,
                biomeCentersT      = biomeCentersT,
                biomeCentersM      = biomeCentersM,
                mountainRegionIndex = settings.mountainRegionIndex,
                heightMap          = heightResult,
                biomeMap           = biomeResult
            }.Schedule(totalCells, 64).Complete();

            ChunkData chunkData = new();
            FillChunkArrays(chunkData);

            if (settings.endlessFlag[2])
                SpawnObjects(centre, chunkData, chunkRandom);

            return chunkData;
        }

        private void FillChunkArrays(ChunkData cd)
        {
            noiseMap.CopyTo(cd.HeightRaw);
            heightResult.CopyTo(cd.HeightMap);
            biomeResult.CopyTo(cd.BiomeMap);

            for (int i = 0; i < totalCells; i++)
                cd.HeightRaw[i] *= maxTerrainHeight;
        }

        private void SpawnObjects(
            Vector2Int centre,
            ChunkData chunkData,
            FastRandom chunkRandom)
        {
            int regionCount = settings.regions.Length;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    int idx       = Idx(x, y);
                    float rawH    = chunkData.HeightRaw[idx];
                    int regionIdx = chunkData.HeightMap[idx];
                    int biomeIdx  = chunkData.BiomeMap[idx];

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

                        Vector2Int worldPos = new(
                            centre.x * generationSize + x * scale,
                            (centre.y - 1) * generationSize + y * scale);

                        Vector3 realPos = new(worldPos.x, rawH, worldPos.y);

                        var info = new ObjectInstInfo(realPos, gen.info.PrefabID,
                                      gen.info.DefaultAmount, gen.info.InstanceType);

                        int[] additionalIdxs = BuildAdditionalLocalIdxs(
                            x, y, gen.info.AdditionalOccupiedCells);

                        if (gen.info.AdditionalOccupiedCells?.Length > 0 && additionalIdxs == null)
                            continue;

                        if (chunkData.TryAddObject(idx, in info, additionalIdxs))
                            break;
                    }
                }
            }
        }

        private static int[] BuildAdditionalLocalIdxs(int x, int y, Vector2Int[] worldOffsets)
        {
            if (worldOffsets == null || worldOffsets.Length == 0)
                return null;

            var result = new int[worldOffsets.Length];

            for (int i = 0; i < worldOffsets.Length; i++)
            {
                int lx = x + worldOffsets[i].x / scale;
                int ly = y + worldOffsets[i].y / scale;

                // Смещение вышло за границы чанка — многоклеточный объект не влезает
                if ((uint)lx >= (uint)mapChunkSize || (uint)ly >= (uint)mapChunkSize)
                    return null;

                result[i] = Idx(lx, ly);
            }

            return result;
        }
        private void ApplyBiomeModifiers()
        {
            BiomeSettings[] biomes = settings.biomesSettings;
            int biomeCount = biomes.Length;

            const float blendRadius    = 0.25f;
            const float rcpR2          = 1f / (blendRadius * blendRadius);
            const float altitudeCooling = 0.35f;

            Array.Clear(chunkBiomeWeights, 0, biomeCount);
            float totalChunkWeight = 0f;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    int idx   = Idx(x, y);
                    float baseH = noiseMap[idx];

                    float temp = Mathf.Clamp01(
                        temperatureMap[idx] - baseH * altitudeCooling);

                    float moisture = moistureMap[idx];

                    float totalW        = 0f;
                    float blendedScale  = 0f;
                    float blendedOffset = 0f;
                    float blendedRiverT = 0f;
                    float blendedBedH   = 0f;
                    float rv = riverMap[idx];

                    for (int b = 0; b < biomeCount; b++)
                    {
                        float dt = temp - biomeCentersT[b];
                        float dm = moisture - biomeCentersM[b];
                        float d2 = (dt * dt + dm * dm) * rcpR2;
                        float w  = math.saturate(1f - d2);
                        w = w * w * (3f - 2f * w);

                        totalW          += w;
                        blendedScale    += w * biomes[b].heightScale;
                        blendedOffset   += w * biomes[b].heightOffset;

                        if (biomes[b].hasRivers)
                        {
                            RiverBlendSettings rs = biomes[b].riverBlend;
                            float riverCenter  = (rs.riverMin + rs.riverMax) * 0.5f;
                            float totalRadius  = (rs.riverMax - rs.riverMin) * 0.5f + rs.influenceWidth;
                            float dist = Mathf.Abs(rv - riverCenter);

                            if (dist < totalRadius)
                            {
                                float t = 1f - dist / totalRadius;
                                t = t * t * (3f - 2f * t);
                                blendedRiverT += w * t;
                                blendedBedH   += w * rs.riverBedHeight;
                            }
                        }

                        chunkBiomeWeights[b] += w;
                        totalChunkWeight     += w;
                    }

                    float rcpW;
                    if (totalW > 0.0001f)
                    {
                        rcpW = 1f / totalW;
                    }
                    else
                    {
                        rcpW          = 1f;
                        blendedScale  = biomes[0].heightScale;
                        blendedOffset = biomes[0].heightOffset;
                    }

                    float h = Mathf.Clamp01(
                        baseH * (blendedScale * rcpW) + blendedOffset * rcpW);

                    float riverT = blendedRiverT * rcpW;
                    if (riverT > 0f)
                        h = Mathf.LerpUnclamped(h, blendedBedH * rcpW, riverT);

                    noiseMap[idx] = h;
                }
            }

            if (totalChunkWeight > 0f)
            {
                float rcp = 1f / totalChunkWeight;
                for (int b = 0; b < biomeCount; b++)
                    chunkBiomeWeights[b] *= rcp;
            }
        }

        public void ApplyErosion(Vector2Int chunk)
        {
            if (!settings.erosion.enabled) return;

            int seed =
                settings.seed ^
                (chunk.x * 73856093) ^
                (chunk.y * 19349663);

            new HydraulicErosionJob
            {
                mapSize   = MapGenerator.mapChunkSize,
                seed      = seed,
                settings  = settings.erosion,
                heightMap = noiseMap
            }.Schedule().Complete();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Idx(int x, int y) => y * mapChunkSize + x;
        public void Dispose()
        {
            if (noiseMap.IsCreated)       noiseMap.Dispose();
            if (riverMap.IsCreated)       riverMap.Dispose();
            if (temperatureMap.IsCreated) temperatureMap.Dispose();
            if (moistureMap.IsCreated)    moistureMap.Dispose();
            if (heightResult.IsCreated)   heightResult.Dispose();
            if (biomeResult.IsCreated)    biomeResult.Dispose();
            if (biomeCentersT.IsCreated)  biomeCentersT.Dispose();
            if (biomeCentersM.IsCreated)  biomeCentersM.Dispose();
            if (regionThresholds.IsCreated) regionThresholds.Dispose();
        }
    }
}