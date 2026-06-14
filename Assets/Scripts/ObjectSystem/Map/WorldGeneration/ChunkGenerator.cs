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
        private const int totalCells     = mapChunkSize * mapChunkSize;
        private const float maxTerrainHeight = MapGenerator.maxTerrainHeight;

        private readonly ChunkGenerationSettings settings;
        private readonly Noise noise;
        private NativeArray<float> noiseMap;
        private NativeArray<float> deltaMap;
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

        private readonly NativeArray<byte> moveCost;
        private NativeArray<ObjectSpawnConfig> spawnConfigs;
        private NativeArray<ObjectInstInfo> spawnOutput;
        private NativeReference<int> spawnCount;
        private NativeArray<byte> spawnGridBuffer;
                
        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(
            ChunkGenerationSettings settings,
            int seed)
        {
            this.settings     = settings;
            noise = new(
                settings.heightNoiseSettings,
                settings.riverNoiseSettings,
                settings.temperatureSettings,
                settings.moistureSettings,
                seed,
                mapChunkSize);

            noiseMap       = new NativeArray<float>(totalCells, Allocator.Persistent);
            deltaMap       = new NativeArray<float>(totalCells, Allocator.Persistent);

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

            moveCost = new NativeArray<byte>(totalCells, Allocator.Persistent);

            int gridRes = math.min(mapChunkSize * scale, 256);
            spawnOutput     = new NativeArray<ObjectInstInfo>(settings.maxObjectsPerChunk, Allocator.Persistent);
            spawnCount      = new NativeReference<int>(0, Allocator.Persistent);
            spawnGridBuffer = new NativeArray<byte>(gridRes * gridRes, Allocator.Persistent);
            spawnConfigs    = SpawnConfigBaker.BakeSpawnConfigs(settings.spawnProfiles, Allocator.Persistent);       
        }
        public ChunkData GenerateMapData(long centre)
        {
            uint hash = (uint)centre;
            FastRandom chunkRandom = new(hash);

            noise.GenerateAllMaps(
                noiseMap, riverMap, temperatureMap, moistureMap, Position2Int.UnpackToVector(centre));

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
                }.Schedule(totalCells, 64);

            JobHandle erosionHandle =
                new HydraulicErosionJob
                {
                    mapSize = mapChunkSize,
                    seed = hash,
                    settings = settings.erosion,

                    heightMap = biomeHeightMap,
                    deltaMap = deltaMap
                }.Schedule(biomeHandle);

            JobHandle regionHandle = new RegionAssignJob
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
                }.Schedule(
                    totalCells,
                    64,
                    erosionHandle);

            JobHandle slopeHandle = new SlopeMapJob
                {
                    heightMap     = biomeHeightMap,
                    mapSize       = mapChunkSize,
                    heightScale   = maxTerrainHeight,
                    cellWorldDist = 2f * scale,
                    moveCost      = moveCost,
                }.Schedule(totalCells, 64, regionHandle);
            

            ChunkData chunkData = new();

            if (settings.endlessFlag[2])
            {
                spawnCount.Value = 0;
                int2 chunkOrigin = new(Position2Int.GetX(centre), Position2Int.GetY(centre));
                float chunkWorldSize = mapChunkSize * scale;

                JobHandle spawnHandle = new SpawnLayerJob
                    {
                        configs = spawnConfigs,
                        heightMap = biomeHeightMap,
                        temperatureMap = temperatureMap,
                        moistureMap = moistureMap,
                        chunkOrigin = chunkOrigin,
                        seed = hash,
                        chunkWorldSize = chunkWorldSize,
                        mapChunkSize = mapChunkSize,
                        scale = scale,
                        output = spawnOutput,
                        outputCount = spawnCount,
                        gridBuffer = spawnGridBuffer
                    }.Schedule(slopeHandle);

                spawnHandle.Complete();

                int finalCount = spawnCount.Value;
                for (int i = 0; i < finalCount; i++)
                {
                    ObjectInstInfo inst = spawnOutput[i];
                    float2 chunkWorldOrigin = new float2(chunkOrigin) * chunkWorldSize;
                    int2 cell = (int2)math.floor(
                        (new float2(inst.Position.x, inst.Position.z) - chunkWorldOrigin) / scale);

                    int idx = math.clamp(cell.y * mapChunkSize + cell.x, 0, totalCells - 1);
                    chunkData.TryAddObject(idx, in inst, null);
                }   
            }
            else
            {
                slopeHandle.Complete();
            }

            FillChunkArrays(chunkData);

            return chunkData;
        }

        private void FillChunkArrays(ChunkData cd)
        {
            biomeHeightMap.CopyTo(cd.HeightRaw);
            biomeResult.CopyTo(cd.BiomeMap);

            moveCost.CopyTo(cd.MoveCost);

            for (int i = 0; i < totalCells; i++)
                cd.HeightRaw[i] *= maxTerrainHeight;
        }
        
        public void Dispose()
        {
            if (noiseMap.IsCreated)         noiseMap.Dispose();
            if (deltaMap.IsCreated)         deltaMap.Dispose();

            if (riverMap.IsCreated)         riverMap.Dispose();
            if (temperatureMap.IsCreated)   temperatureMap.Dispose();
            if (moistureMap.IsCreated)      moistureMap.Dispose();
            if (heightResult.IsCreated)     heightResult.Dispose();
            if (biomeResult.IsCreated)      biomeResult.Dispose();
            if (biomeCentersT.IsCreated)    biomeCentersT.Dispose();
            if (biomeCentersM.IsCreated)    biomeCentersM.Dispose();
            if (regionThresholds.IsCreated) regionThresholds.Dispose();


            if (biomeHeightMap.IsCreated)   biomeHeightMap.Dispose();
            if (biomeHeightScale.IsCreated) biomeHeightScale.Dispose();
            if (biomeHeightOffset.IsCreated)biomeHeightOffset.Dispose();
            if (biomeHasRiver.IsCreated)    biomeHasRiver.Dispose();
            if (riverCenter.IsCreated)      riverCenter.Dispose();
            if (riverRadius.IsCreated)      riverRadius.Dispose();
            if (riverRadiusRcp.IsCreated)   riverRadiusRcp.Dispose();
            if (riverBedHeight.IsCreated)   riverBedHeight.Dispose();

            if (moveCost.IsCreated)         moveCost.Dispose();

            if (spawnConfigs.IsCreated)     spawnConfigs.Dispose();
            if (spawnOutput.IsCreated)      spawnOutput.Dispose();
            if (spawnCount.IsCreated)       spawnCount.Dispose();
            if (spawnGridBuffer.IsCreated)  spawnGridBuffer.Dispose();

        }
    }
}