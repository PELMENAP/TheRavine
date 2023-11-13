using UnityEngine;
using System;
using System.Collections.Generic;

public class MapGeneratorOld : MonoBehaviour
{
    public static int seed;
    [SerializeField] private float noiseScale;
    [SerializeField] private int octaves;
    [SerializeField, Range(0, 1)] private float persistance;
    [SerializeField] private float lacunarity;
    [SerializeField] private TerrainType[] regions;
    private Dictionary<Vector2, MapData> mapData = new Dictionary<Vector2, MapData>();
    // private int mapChunkSize = 16;

    // private void Awake()
    // {
    //     for (int i = -10; i < 10; i++)
    //     {
    //         for (int j = -10; j < 10; j++)
    //         {
    //             mapData[new Vector2(i * mapChunkSize, j * mapChunkSize)] = GenerateMapData(new Vector2(i * mapChunkSize, j * mapChunkSize));
    //         }
    //     }
    // }

    // public MapData GenerateMapData(Vector2 centre)
    // {
    //     // float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre, Noise.NormalizeMode.Global);
    //     Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
    //     int[,] objectMap = new int[mapChunkSize, mapChunkSize];
    //     float currentHeight;
    //     for (int y = 0; y < mapChunkSize; y++)
    //     {
    //         for (int x = 0; x < mapChunkSize; x++)
    //         {
    //             currentHeight = noiseMap[x, y];
    //             for (int i = 0; i < regions.Length; i++)
    //             {
    //                 if (currentHeight >= regions[i].height)
    //                 {
    //                     colourMap[y * mapChunkSize + x] = regions[i].colour;
    //                     objectMap[x, y] = i;
    //                 }
    //                 else
    //                 {
    //                     break;
    //                 }
    //             }
    //         }
    //     }
    //     noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed * 2, mapChunkSize, 5, 0.3f, 3, centre, Noise.NormalizeMode.Local);
    //     for (int i = 0; i < mapChunkSize; i++)
    //     {
    //         for (int j = 0; j < mapChunkSize; j++)
    //         {
    //             noiseMap[i, j] = (int)(noiseMap[i, j] * 100);
    //         }
    //     }
    //     return new MapData(noiseMap, colourMap, objectMap);
    // }


    public MapData GetMapData(Vector2 centre) => mapData[centre];
}

[System.Serializable]

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colourMap;
    public readonly int[,] objectMap;
    public MapData(float[,] heightMap, Color[] colourMap, int[,] objectMap)
    {
        this.heightMap = heightMap;
        this.colourMap = colourMap;
        this.objectMap = objectMap;
    }
}