using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Events;

using TheRavine.Extensions;

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
        private readonly int[] ringConfigEnd;
        private readonly bool spawnEnabled;
        private readonly int worldSeed;
                
        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(
            ChunkGenerationSettings settings,
            int seed)
        {
            this.settings     = settings;
            worldSeed         = seed;
            spawnEnabled      = settings.endlessFlag != null && settings.endlessFlag.Length > 2 && settings.endlessFlag[2];
            noise = new(
                settings.heightNoiseSettings,
                settings.riverNoiseSettings,
                settings.temperatureSettings,
                settings.moistureSettings,
                seed);

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

            spawnOutput     = new NativeArray<ObjectInstInfo>(math.max(1, settings.maxObjectsPerChunk), Allocator.Persistent);
            spawnCount      = new NativeReference<int>(0, Allocator.Persistent);
            spawnGridBuffer = new NativeArray<byte>(totalCells, Allocator.Persistent);
            spawnConfigs    = SpawnConfigBaker.BakeSpawnConfigs(settings.spawnProfiles, Allocator.Persistent, out ringConfigEnd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ChunkSeed(int x, int z) => math.hash(new int3(x, z, worldSeed));

        public void Generate(ChunkData chunkData, long centre, int ring)
        {
            ring = math.clamp(ring, 0, ObjectInfo.MaxVisibleRings);
            int2 chunkOrigin = new(Position2Int.GetX(centre), Position2Int.GetY(centre));
            uint hash = ChunkSeed(chunkOrigin.x, chunkOrigin.y);

            JobHandle mapHandle = noise.GenerateAllMaps(
                noiseMap, riverMap, temperatureMap, moistureMap,
                Position2Int.UnpackToVector(centre));

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
                }.Schedule(totalCells, 64, mapHandle);

            JobHandle erosionHandle =
                new HydraulicErosionJob
                {
                    seed = hash,
                    settings = settings.erosion,

                    heightMap = biomeHeightMap,
                    deltaMap = deltaMap
                }.Schedule(biomeHandle);

            JobHandle finalizeHandle = new TerrainFinalizeJob
            {
                heightValues = biomeHeightMap,

                temperatureValues = temperatureMap,
                moistureValues = moistureMap,

                regionThresholds = regionThresholds,

                biomeCentersT = biomeCentersT,
                biomeCentersM = biomeCentersM,

                mountainRegionIndex = settings.mountainRegionIndex,

                regionMap = heightResult,
                biomeMap = biomeResult,
                moveCost = moveCost
            }.Schedule(totalCells, 64, erosionHandle);

            JobHandle fillHandle = new FillChunkArraysJob
            {
                height = biomeHeightMap,
                biome = biomeResult,
                moveCostIn = moveCost,
                heightScale = maxTerrainHeight,
                heightRaw = chunkData.HeightRaw,
                biomeMap = chunkData.BiomeMap,
                moveCostOut = chunkData.MoveCost
            }.ScheduleParallel(totalCells, 256, finalizeHandle);

            if (spawnEnabled)
            {
                RunSpawn(chunkData, chunkOrigin, hash, 0, ringConfigEnd[ring], fillHandle);
                chunkData.DetailLevel = (byte)ring;
            }
            else
            {
                fillHandle.Complete();
                chunkData.DetailLevel = 0;
            }

            chunkData.CommitGeneration(false);
        }

        public void RaiseDetail(ChunkData chunkData, long centre, int ring)
        {
            ring = math.clamp(ring, 0, ObjectInfo.MaxVisibleRings);
            if (chunkData.DetailLevel <= ring) return;

            if (!spawnEnabled)
            {
                chunkData.DetailLevel = 0;
                return;
            }

            int begin = ringConfigEnd[math.min((int)chunkData.DetailLevel, ObjectInfo.MaxVisibleRings)];
            int end = ringConfigEnd[ring];
            chunkData.DetailLevel = (byte)ring;
            if (begin >= end) return;

            int2 chunkOrigin = new(Position2Int.GetX(centre), Position2Int.GetY(centre));
            bool wasModified = chunkData.IsModified;

            JobHandle climateHandle = noise.GenerateClimate(
                temperatureMap, moistureMap,
                Position2Int.UnpackToVector(centre));

            RunSpawn(chunkData, chunkOrigin, ChunkSeed(chunkOrigin.x, chunkOrigin.y), begin, end, climateHandle);
            chunkData.CommitGeneration(wasModified);
        }

        private void RunSpawn(ChunkData chunkData, int2 chunkOrigin, uint hash, int firstConfig, int endConfig, JobHandle dependency)
        {
            if (firstConfig >= endConfig)
            {
                dependency.Complete();
                return;
            }

            spawnCount.Value = 0;

            new SpawnLayerJob
            {
                configs = spawnConfigs,
                firstConfig = firstConfig,
                endConfig = endConfig,
                heightRaw = chunkData.HeightRaw,
                temperatureMap = temperatureMap,
                moistureMap = moistureMap,
                existing = chunkData.Objects.AsArray(),
                chunkOrigin = chunkOrigin,
                seed = hash,
                output = spawnOutput,
                outputCount = spawnCount,
                gridBuffer = spawnGridBuffer
            }.Schedule(dependency).Complete();

            int finalCount = spawnCount.Value;
            for (int i = 0; i < finalCount; i++)
            {
                ObjectInstInfo inst = spawnOutput[i];
                chunkData.TryAddObject(inst.PrimaryIdx, in inst);
            }
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

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct FillChunkArraysJob : IJobFor
    {
        [ReadOnly] public NativeArray<float> height;
        [ReadOnly] public NativeArray<int> biome;
        [ReadOnly] public NativeArray<byte> moveCostIn;
        public float heightScale;

        [WriteOnly] public NativeArray<float> heightRaw;
        [WriteOnly] public NativeArray<int> biomeMap;
        [WriteOnly] public NativeArray<byte> moveCostOut;

        public void Execute(int i)
        {
            heightRaw[i] = height[i] * heightScale;
            biomeMap[i] = biome[i];
            moveCostOut[i] = moveCostIn[i];
        }
    }
}
