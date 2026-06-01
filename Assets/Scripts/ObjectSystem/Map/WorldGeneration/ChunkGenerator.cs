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

        private readonly ChunkGenerationSettings settings;
        private NativeArray<float> noiseMap;
        private readonly NativeArray<float> riverMap, temperatureMap, moistureMap;
        private readonly NativeArray<float> biomeHeightMap;
        private readonly NativeArray<int>   heightResult, biomeResult;
        private readonly NativeArray<float> biomeCentersT, biomeCentersM, regionThresholds;

        private readonly NativeArray<float> biomeHeightScale;
        private readonly NativeArray<float> biomeHeightOffset;

        private readonly NativeArray<byte> biomeHasRiver;

        private readonly NativeArray<float> riverCenter;
        private readonly NativeArray<float> riverRadius;
        private readonly NativeArray<float> riverRadiusRcp;
        private readonly NativeArray<float> riverBedHeight;

        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(
            ChunkGenerationSettings settings)
        {
            this.settings     = settings;

            noiseMap       = new NativeArray<float>(totalCells, Allocator.Persistent);
            riverMap       = new NativeArray<float>(totalCells, Allocator.Persistent);
            temperatureMap = new NativeArray<float>(totalCells, Allocator.Persistent);
            moistureMap    = new NativeArray<float>(totalCells, Allocator.Persistent);

            heightResult = new NativeArray<int>(totalCells, Allocator.Persistent);
            biomeResult  = new NativeArray<int>(totalCells, Allocator.Persistent);

            int biomeCount = settings.biomesSettings?.Length ?? 0;

            biomeCentersT     = new NativeArray<float>(biomeCount, Allocator.Persistent);
            biomeCentersM     = new NativeArray<float>(biomeCount, Allocator.Persistent);

            for (int i = 0; i < biomeCount; i++)
            {
                Vector2 center = settings.biomesSettings[i].Center;
                biomeCentersT[i] = center.x;
                biomeCentersM[i] = center.y;
            }

            regionThresholds = new NativeArray<float>(settings.regions.Length, Allocator.Persistent);
            for (int i = 0; i < settings.regions.Length; i++)
                regionThresholds[i] = settings.regions[i].height;

            biomeHeightMap = new NativeArray<float>(totalCells, Allocator.Persistent);

            biomeHeightScale = new NativeArray<float>(biomeCount, Allocator.Persistent);
            biomeHeightOffset = new NativeArray<float>(biomeCount, Allocator.Persistent);

            biomeHasRiver = new NativeArray<byte>(biomeCount, Allocator.Persistent);

            riverCenter = new NativeArray<float>(biomeCount, Allocator.Persistent);
            riverRadius = new NativeArray<float>(biomeCount, Allocator.Persistent);
            riverRadiusRcp = new NativeArray<float>(biomeCount, Allocator.Persistent);

            riverBedHeight = new NativeArray<float>(biomeCount, Allocator.Persistent);

            for (int i = 0; i < biomeCount; i++)
            {
                var biome = settings.biomesSettings[i];

                biomeHeightScale[i] = biome.heightScale;
                biomeHeightOffset[i] = biome.heightOffset;

                biomeHasRiver[i] =
                    biome.hasRivers
                        ? (byte)1
                        : (byte)0;

                RiverBlendSettings rs = biome.riverBlend;

                float center =
                    (rs.riverMin + rs.riverMax) * 0.5f;

                float radius =
                    (rs.riverMax - rs.riverMin) * 0.5f +
                    rs.influenceWidth;

                riverCenter[i] = center;
                riverRadius[i] = radius;
                riverRadiusRcp[i] =
                    radius > 0f
                        ? math.rcp(radius)
                        : 0f;

                riverBedHeight[i] =
                    rs.riverBedHeight;
            }
        }
        public ChunkData GenerateMapData(Vector2Int centre)
        {
            int hash =
                settings.seed ^
                (centre.x * 73856093) ^
                (centre.y * 19349663);

            FastRandom chunkRandom = new(hash);

            Noise.GenerateAllMaps(
                noiseMap, riverMap, temperatureMap, moistureMap,
                centre);

            JobHandle biomeHandle =
                new BiomeModifierJob
                {
                    heightIn = noiseMap,
                    riverMap = riverMap,

                    temperatureMap = temperatureMap,
                    moistureMap = moistureMap,

                    biomeCentersT = biomeCentersT,
                    biomeCentersM = biomeCentersM,

                    biomeHeightScale = biomeHeightScale,
                    biomeHeightOffset = biomeHeightOffset,

                    biomeHasRiver = biomeHasRiver,

                    riverCenter = riverCenter,
                    riverRadius = riverRadius,
                    riverRadiusRcp = riverRadiusRcp,
                    riverBedHeight = riverBedHeight,

                    blendRadiusRcp2 =
                        math.rcp(settings.biomeBlendRadius *
                                settings.biomeBlendRadius),

                    altitudeCooling =
                        settings.altitudeCooling,

                    heightOut = biomeHeightMap
                }
                .Schedule(totalCells, 64);

            JobHandle erosionHandle =
                new HydraulicErosionJob
                {
                    mapSize = mapChunkSize,
                    seed = hash,
                    settings = settings.erosion,

                    heightMap = biomeHeightMap
                }
                .Schedule(biomeHandle);

            JobHandle regionHandle =
                new RegionAssignJob
                {
                    heightValues = biomeHeightMap,

                    temperatureValues = temperatureMap,
                    moistureValues = moistureMap,

                    regionThresholds = regionThresholds,

                    biomeCentersT = biomeCentersT,
                    biomeCentersM = biomeCentersM,

                    mountainRegionIndex =
                        settings.mountainRegionIndex,

                    heightMap = heightResult,
                    biomeMap = biomeResult
                }
                .Schedule(
                    totalCells,
                    64,
                    erosionHandle);
            
            regionHandle.Complete();

            ChunkData chunkData = new();
            FillChunkArrays(chunkData);

            if (settings.endlessFlag[2])
                SpawnObjects(centre, chunkData, chunkRandom);

            return chunkData;
        }

        private void FillChunkArrays(ChunkData cd)
        {
            biomeHeightMap.CopyTo(cd.HeightRaw);
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
                    int regionIdx = heightResult[idx];
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


            if (biomeHeightMap.IsCreated) biomeHeightMap.Dispose();
            if (biomeHeightScale.IsCreated) biomeHeightScale.Dispose();
            if (biomeHeightOffset.IsCreated) biomeHeightOffset.Dispose();
            if (biomeHasRiver.IsCreated)  biomeHasRiver.Dispose();
            if (riverCenter.IsCreated)    riverCenter.Dispose();
            if (riverRadius.IsCreated)    riverRadius.Dispose();
            if (riverRadiusRcp.IsCreated) riverRadiusRcp.Dispose();
            if (riverBedHeight.IsCreated) riverBedHeight.Dispose();
        }
    }
}