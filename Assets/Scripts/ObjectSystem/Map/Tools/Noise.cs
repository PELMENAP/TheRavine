using UnityEngine;
using TheRavine.Generator;
using Unity.Collections;
using System.Runtime.CompilerServices;

public static class Noise
{
    private const int mapChunkSize = MapGenerator.mapChunkSize;
    private static FastNoiseLite _heightNoise;
    private static FastNoiseLite _riverNoise;
    private static FastNoiseLite _domainWarp;
    private static FastNoiseLite _temperatureNoise;
    private static FastNoiseLite _moistureNoise;

    private static int _chunkSize;

    public static void SetInit(
        NoiseLayerSettings heightSettings,
        NoiseLayerSettings riverSettings,
        NoiseLayerSettings temperatureSettings,
        NoiseLayerSettings moistureSettings,
        int seed,
        int chunkSize)
    {
        _chunkSize = chunkSize;
        _heightNoise = BuildNoise(heightSettings, seed);
        _riverNoise = BuildNoise(riverSettings, seed * 2);
        _temperatureNoise = BuildNoise(temperatureSettings, seed * 4);
        _moistureNoise    = BuildNoise(moistureSettings,    seed * 5);

        _domainWarp = new FastNoiseLite(seed * 3);
        _domainWarp.SetDomainWarpType(FastNoiseLite.DomainWarpType.OpenSimplex2);
        _domainWarp.SetFractalType(FastNoiseLite.FractalType.DomainWarpProgressive);
        _domainWarp.SetFractalOctaves(2);
    }

    private static FastNoiseLite BuildNoise(in NoiseLayerSettings s, int seed)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(s.noiseType);
        noise.SetFractalType(s.fractalType);
        noise.SetFrequency(s.frequency);
        noise.SetFractalOctaves(s.octaves);
        noise.SetFractalLacunarity(s.lacunarity);
        noise.SetFractalGain(s.gain);
        noise.SetFractalWeightedStrength(s.weightedStrength);
        return noise;
    }

    public static void GenerateHeightMap(NativeArray<float> noiseMap, Vector2Int chunkOffset)
    {
        FillMap(noiseMap, _heightNoise, chunkOffset);
    }

    public static void GenerateRiverMap(NativeArray<float> noiseMap, Vector2Int chunkOffset)
    {
        FillMap(noiseMap, _riverNoise, chunkOffset);
    }

    public static void GenerateClimateMap(
        NativeArray<float> temperatureMap,
        NativeArray<float> moistureMap,
        Vector2Int chunkOffset)
    {
        int worldX = chunkOffset.x * _chunkSize;
        int worldY = chunkOffset.y * _chunkSize;
        for (int y = 0; y < _chunkSize; y++)
        {
            int wy = worldY + y;
            for (int x = 0; x < _chunkSize; x++)
            {
                int wx = worldX + x;
                temperatureMap[Idx(x, y)] = _temperatureNoise.GetNoise(wx, wy) * 0.5f + 0.5f;
                moistureMap[Idx(x, y)]    = _moistureNoise.GetNoise(wx, wy)    * 0.5f + 0.5f;
            }
        }
    }
    private static void FillMap(NativeArray<float> noiseMap, FastNoiseLite noise, Vector2Int chunkOffset)
    {
        int worldX = chunkOffset.x * _chunkSize;
        int worldY = chunkOffset.y * _chunkSize;

        for (int y = 0; y < _chunkSize; y++)
        {
            int wy = worldY + y;
            for (int x = 0; x < _chunkSize; x++)
            {
                noiseMap[Idx(x, y)] = noise.GetNoise(worldX + x, wy) * 0.5f + 0.5f;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Idx(int x, int y)
    {
        return y * mapChunkSize + x;
    }
}