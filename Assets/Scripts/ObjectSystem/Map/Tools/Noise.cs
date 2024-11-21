using UnityEngine;

using TheRavine.Generator;
using TheRavine.Extensions;
public static class Noise
{
    public enum NormalizeMode { Local, Global, Temp };
    private static Vector2[] octaveOffsets, tempOctaveOffsets;
    private static byte octaves, halfWidth = MapGenerator.mapChunkSize / 2, halfHeight = MapGenerator.mapChunkSize / 2;
    private static float persistence, lacunarity, scaleInverse;
    public static void SetInit(float scale, byte _octaves, float _persistence, float _lacunarity, int _seed)
    {
        FastRandom prng = new FastRandom(_seed);
        FastRandom tprng = new FastRandom(_seed * 2);
        octaveOffsets = new Vector2[_octaves];
        tempOctaveOffsets = new Vector2[_octaves];

        octaves = _octaves;
        persistence = _persistence;
        lacunarity = _lacunarity;
        scaleInverse = 1 / scale;

        for (byte i = 0; i < octaves; i++)
        {
            octaveOffsets[i] = new Vector2(prng.Range(-100000, 100000), prng.Range(-100000, 100000));
            tempOctaveOffsets[i] = new Vector2(tprng.Range(-100000, 100000), tprng.Range(-100000, 100000));
        }
    }
    public static void GenerateNoiseMap(ref float[,] noiseMap, Vector2 offset, NormalizeMode normalizeMode)
    {
        float maxPossibleHeight = 0;
        float amplitude = 1;

        for (byte i = 0; i < octaves; i++)
        {
            maxPossibleHeight += amplitude;
            amplitude *= persistence;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        for (byte y = 0; y < MapGenerator.mapChunkSize; y++)
        {
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
            {
                amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
                for (byte i = 0; i < octaves; i++)
                {
                    float sampleX = (x + halfWidth + octaveOffsets[i].x + offset.x) * scaleInverse * frequency;
                    float sampleY = (y + halfHeight + octaveOffsets[i].y + offset.y) * scaleInverse * frequency;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }
                if (noiseHeight > maxLocalNoiseHeight)
                    maxLocalNoiseHeight = noiseHeight;
                else if (noiseHeight < minLocalNoiseHeight)
                    minLocalNoiseHeight = noiseHeight;
                noiseMap[x, y] = noiseHeight;
            }
        }

        float globalNormalizeFactor = 1 / (maxPossibleHeight / 0.9f);
        for (byte y = 0; y < MapGenerator.mapChunkSize; y++)
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
                if (normalizeMode == NormalizeMode.Local)
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                else
                    noiseMap[x, y] = Mathf.Clamp((noiseMap[x, y] + 1) * globalNormalizeFactor, 0, int.MaxValue);
    }

    public static void GenerateTempNoiseMap(ref float[,] noiseMap, Vector2 offset)
    {
        float maxPossibleHeight = 0;
        float amplitude = 1;

        for (byte i = 0; i < octaves; i++)
        {
            maxPossibleHeight += amplitude;
            amplitude *= persistence;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        for (byte y = 0; y < MapGenerator.mapChunkSize; y++)
        {
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
            {
                amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
                for (byte i = 0; i < octaves; i++)
                {
                    float sampleX = (x + halfWidth + tempOctaveOffsets[i].x + offset.x) * 0.5f * scaleInverse * frequency;
                    float sampleY = (y + halfHeight + tempOctaveOffsets[i].y + offset.y) * 0.5f * scaleInverse * frequency;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }
                if (noiseHeight > maxLocalNoiseHeight)
                    maxLocalNoiseHeight = noiseHeight;
                else if (noiseHeight < minLocalNoiseHeight)
                    minLocalNoiseHeight = noiseHeight;
                noiseMap[x, y] = noiseHeight;
            }
        }

        float globalNormalizeFactor = 1 / (maxPossibleHeight / 0.9f);
        for (byte y = 0; y < MapGenerator.mapChunkSize; y++)
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
                    noiseMap[x, y] = Mathf.Clamp((noiseMap[x, y] + 1) * globalNormalizeFactor, 0, int.MaxValue);
    }
}