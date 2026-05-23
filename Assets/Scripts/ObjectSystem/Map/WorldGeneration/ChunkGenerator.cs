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
    public class ChunkGenerator
    {
        private const int mapChunkSize = MapGenerator.mapChunkSize;
        private const int scale = MapGenerator.scale;
        private const int generationSize = scale * mapChunkSize;

        private readonly ObjectSystem _objectSystem;
        private readonly ChunkGenerationSettings _settings;

        // Reused across calls — no alloc per chunk
        private float[,] _noiseMap = new float[mapChunkSize, mapChunkSize];
        private float[,] _riverMap = new float[mapChunkSize, mapChunkSize];

        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;

        public ChunkGenerator(ObjectSystem objectSystem, ChunkGenerationSettings settings)
        {
            _objectSystem = objectSystem;
            _settings = settings;
        }

        public ChunkData GenerateMapData(Vector2Int centre)
        {
            FastRandom chunkRandom = new(_settings.seed + centre.x + centre.y);

            Noise.GenerateHeightMap(ref _noiseMap, centre);
            Noise.GenerateRiverMap(ref _riverMap, centre);

            if (_settings.isRiver)
                Noise.CombineMaps(ref _noiseMap, _riverMap, in _settings.riverBlend);

            int totalCells = mapChunkSize * mapChunkSize;

            using NativeArray<float> heightNative = new(totalCells, Allocator.TempJob);
            using NativeArray<float> riverNative = new(totalCells, Allocator.TempJob);
            using NativeArray<int> heightResult = new(totalCells, Allocator.TempJob);
            using NativeArray<int> tempResult = new(totalCells, Allocator.TempJob);

            Flatten(_noiseMap, heightNative);
            Flatten(_riverMap, riverNative);

            NativeArray<float> regionHeights = new(_settings.regions.Length, Allocator.TempJob);
            NativeArray<float> biomHeights = new(_settings.biomRegions.Length, Allocator.TempJob);

            try
            {
                for (int i = 0; i < _settings.regions.Length; i++)
                    regionHeights[i] = _settings.regions[i].height;
                for (int i = 0; i < _settings.biomRegions.Length; i++)
                    biomHeights[i] = _settings.biomRegions[i].height;
                
                var job = new RegionAssignJob
                {
                    heightValues = heightNative,
                    riverValues = riverNative,
                    regionThresholds = regionHeights,
                    biomThresholds = biomHeights,
                    mountainRegionIndex = _settings.mountainRegionIndex,
                    heightMap = heightResult,
                    tempMap = tempResult
                };

                job.Schedule(totalCells, 64).Complete();
            }
            finally
            {
                regionHeights.Dispose();
                biomHeights.Dispose();
            }


            int[,] heightMap = new int[mapChunkSize, mapChunkSize];
            int[,] temperatureMap = new int[mapChunkSize, mapChunkSize];
            Unflatten(heightResult, heightMap);
            Unflatten(tempResult, temperatureMap);

            SortedSet<Vector2Int> objectsToInst = new(new Vector2IntComparer());

            if (_settings.endlessFlag[2])
                SpawnObjects(centre, heightMap, temperatureMap, objectsToInst, chunkRandom);

            return new ChunkData(heightMap, temperatureMap, objectsToInst);
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
            Vector2Int centre,
            int[,] heightMap,
            int[,] temperatureMap,
            SortedSet<Vector2Int> objectsToInst,
            FastRandom chunkRandom)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                for (int y = 0; y < mapChunkSize; y++)
                {
                    TemperatureLevel level = _settings.regions[heightMap[x, y]].level[temperatureMap[x, y]];

                    bool structHere = false;

                    if (structHere) continue;

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
            [ReadOnly] public NativeArray<float> riverValues;
            [ReadOnly] public NativeArray<float> regionThresholds;
            [ReadOnly] public NativeArray<float> biomThresholds;
            [ReadOnly] public int mountainRegionIndex;

            [WriteOnly] public NativeArray<int> heightMap;
            [WriteOnly] public NativeArray<int> tempMap;

            public void Execute(int index)
            {
                float h = heightValues[index];
                int regionIndex = 0;
                for (int i = 0; i < regionThresholds.Length; i++)
                {
                    if (h >= regionThresholds[i])
                        regionIndex = i;
                    else
                        break;
                }
                heightMap[index] = regionIndex;

                if (regionIndex == mountainRegionIndex)
                {
                    tempMap[index] = 0;
                    return;
                }

                float r = riverValues[index];
                int biomIndex = 0;
                for (int i = 0; i + 1 < biomThresholds.Length; i++)
                {
                    if (r >= biomThresholds[i + 1])
                        biomIndex = i + 1;
                    else
                        break;
                }
                tempMap[index] = biomIndex;
            }
        }
    }
}