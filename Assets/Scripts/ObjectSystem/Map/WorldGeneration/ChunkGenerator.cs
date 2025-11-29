using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public class ChunkGenerator
    {
        private const int mapChunkSize = MapGenerator.mapChunkSize;
        private const int scale = MapGenerator.scale, generationSize = scale * mapChunkSize;
        private readonly ObjectSystem objectSystem;
        private readonly ChunkGenerationSettings chunkGenerationSettings;
        public ChunkGenerator(ObjectSystem objectSystem, ChunkGenerationSettings chunkGenerationSettings)
        {
            this.objectSystem = objectSystem;
            this.chunkGenerationSettings = chunkGenerationSettings;
        }
        private float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
        private float[,] noiseTemperatureMap = new float[mapChunkSize, mapChunkSize];
        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;
        public ChunkData GenerateMapData(Vector2Int centre)
        {
            FastRandom chunkRandom = new(chunkGenerationSettings.seed + centre.x + centre.y);
            SortedSet<Vector2Int> objectsToInst = new(new Vector2IntComparer());
            int[,] heightMap = new int[mapChunkSize, mapChunkSize];
            int[,] temperatureMap = new int[mapChunkSize, mapChunkSize];
            float[,] chunkNoiseMap = new float[mapChunkSize, mapChunkSize];


            if (Mathf.Abs(centre.x) > chunkGenerationSettings.farlands || Mathf.Abs(centre.y) > chunkGenerationSettings.farlands)
                Noise.GenerateNoiseMap(ref noiseMap, centre * mapChunkSize, Noise.NormalizeMode.Local);
            else
                Noise.GenerateNoiseMap(ref noiseMap, centre * mapChunkSize, Noise.NormalizeMode.Global);
            
            Noise.GenerateNoiseMap(ref noiseTemperatureMap, centre * mapChunkSize, Noise.NormalizeMode.Global, true);

            if(chunkGenerationSettings.isRiver)
                Noise.CombineMaps(ref noiseMap, noiseTemperatureMap, chunkGenerationSettings.riverMin, chunkGenerationSettings.riverMax, chunkGenerationSettings.riverInfluence, chunkGenerationSettings.maxRiverDepth);

            for (int x = 0; x < mapChunkSize; x++)
            {
                for (int y = 0; y < mapChunkSize; y++)
                {
                    float currentHeight = noiseMap[x, y];
                    for (int i = 0; i < chunkGenerationSettings.regions.Length; i++)
                    {
                        if (currentHeight >= chunkGenerationSettings.regions[i].height)
                        {
                            heightMap[x, y] = i;

                            chunkNoiseMap[x, y] = currentHeight / 2f;
                            chunkNoiseMap[x, y] *= i > 3 ? 1 : -1;
                        }
                        else
                            break;
                    }
                    currentHeight = noiseTemperatureMap[x, y];
                    if(heightMap[x,y] == 8)
                    {
                        temperatureMap[x,y] = 0;
                        continue;
                    }
                    for (int i = 0; i + 1 < chunkGenerationSettings.biomRegions.Length; i++)
                    {
                        if (!(currentHeight >= chunkGenerationSettings.biomRegions[i + 1].height))
                        {
                            temperatureMap[x, y] = i;
                            break;
                        }
                    }
                }
            }

            if (chunkGenerationSettings.endlessFlag[2])
                for (int x = 0; x < mapChunkSize; x++)
                {
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        bool structHere = false;
                        TemperatureLevel level = chunkGenerationSettings.regions[heightMap[x, y]].level[temperatureMap[x, y]];
                        // for (int i = 0; i < level.structs.Length; i++)
                        // {
                        //     StructInfoGeneration sinfo = level.structs[i];
                        //     if(sinfo.Chance == 0)
                        //         continue;
                        //     if ((x * y + centre.x * centre.y + Seed + i * countOfHeights[heightMap[x, y]] + count) % sinfo.Chance == 0)
                        //     {
                        //         Vector2Int posstruct = new(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale);
                        //         var WFCAobjects = WFCA(posstruct, (int)((Seed + (int)x + (int)y) % sinfo.info.tileInfo.Length), sinfo.info);
                        //         foreach (var item in WFCAobjects)
                        //         {
                        //             if (objectSystem.TryAddToGlobal(item.Key, item.Value.prefabID, item.Value.amount, item.Value.iType, (x + y) % 2 == 0))
                        //                 {
                        //                     var data = GetMapDataByObjectPosition(item.Key);
                        //                     objectsToInst.Add(item.Key);
                        //                 }
                        //         }
                        //         structHere = true;
                        //         if (sinfo.isSpawnPoint)
                        //             onSpawnPoint?.Invoke(posstruct, heightMap[x, y], temperatureMap[x, y], centre);
                        //         break;
                        //     }
                        // }
                        if (structHere) continue;
                        for (int i = 0; i < level.objects.Length; i++)
                        {
                            ObjectInfoGeneration objectInfoGeneration = level.objects[i];
                            if(objectInfoGeneration.Chance == 0 || objectInfoGeneration.info == null) continue;
                            if (chunkRandom.Range(0, chunkGenerationSettings.rareness) < objectInfoGeneration.Chance)
                            {
                                Vector2Int posobj = new(centre.x * generationSize + x * scale, (centre.y - 1) * generationSize + y * scale);
                                Vector3 realPosition = new(posobj.x, heightMap[x, y] + chunkNoiseMap[x, y], posobj.y);
                                if (objectSystem.TryAddToGlobal(posobj, realPosition, objectInfoGeneration.info.PrefabID, objectInfoGeneration.info.DefaultAmount, objectInfoGeneration.info.InstanceType))
                                {
                                    objectsToInst.Add(posobj);
                                    break;
                                }
                            }
                        }
                    }
                }
            return new ChunkData(chunkNoiseMap, heightMap, temperatureMap, objectsToInst);
        }   
    }

}