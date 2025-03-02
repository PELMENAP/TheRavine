using UnityEngine;

using TheRavine.Generator;
using TheRavine.Extensions;
public static class Noise
{
    public enum NormalizeMode { Local, Global, Temp };

    private static Vector2[] heightOctaveOffsets;
    private static Vector2[] riverOctaveOffsets;
    private static int octaves, halfWidth = MapGenerator.mapChunkSize / 2, halfHeight = MapGenerator.mapChunkSize / 2;
    private static float persistence, lacunarity, scaleInverse;

    public static void SetInit(float scale, int _octaves, float _persistence, float _lacunarity, int _seed)
    {
        FastRandom heightPrng = new FastRandom(_seed);
        FastRandom riverPrng = new FastRandom(_seed * 2);
        heightOctaveOffsets = new Vector2[_octaves];
        riverOctaveOffsets = new Vector2[_octaves];

        octaves = _octaves;
        persistence = _persistence;
        lacunarity = _lacunarity;
        scaleInverse = 1 / scale;

        for (int i = 0; i < octaves; i++)
        {
            heightOctaveOffsets[i] = new Vector2(heightPrng.Range(-100000, 100000), heightPrng.Range(-100000, 100000));
            riverOctaveOffsets[i] = new Vector2(riverPrng.Range(-100000, 100000), riverPrng.Range(-100000, 100000));
        }
    }

    public static void GenerateNoiseMap(ref float[,] noiseMap, Vector2 offset, NormalizeMode normalizeMode, bool isRiver = false)
    {
        Vector2[] octaveOffsets = isRiver ? riverOctaveOffsets : heightOctaveOffsets;

        float maxPossibleHeight = 0;
        float amplitude = 1;

        for (int i = 0; i < octaves; i++)
        {
            maxPossibleHeight += amplitude;
            amplitude *= persistence * (isRiver ? 2 : 1);
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        for (int y = 0; y < MapGenerator.mapChunkSize; y++)
        {
            for (int x = 0; x < MapGenerator.mapChunkSize; x++)
            {
                amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x + halfWidth + octaveOffsets[i].x + offset.x) * scaleInverse * frequency * (isRiver ? 0.5f : 1);
                    float sampleY = (y + halfHeight + octaveOffsets[i].y + offset.y) * scaleInverse * frequency * (isRiver ? 0.5f : 1);
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
        for (int y = 0; y < MapGenerator.mapChunkSize; y++)
            for (int x = 0; x < MapGenerator.mapChunkSize; x++)
                if (normalizeMode == NormalizeMode.Local)
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                else
                    noiseMap[x, y] = Mathf.Clamp((noiseMap[x, y] + 1) * globalNormalizeFactor, 0, 1);
    }

    public static void CombineMaps(ref float[,] heightMap, float[,] riverNoiseMap, float riverMin, float riverMax, float riverInfluence, float maxRiverDepth)
    {
        for (int y = 0; y < MapGenerator.mapChunkSize; y++)
        {
            for (int x = 0; x < MapGenerator.mapChunkSize; x++)
            {
                if (riverNoiseMap[x, y] > riverMin && riverNoiseMap[x, y] < riverMax)
                {
                    heightMap[x, y] = 0.05f;
                }
                else if(heightMap[x, y] < 0.2f)
                {
                    continue;
                }
                else if(riverNoiseMap[x, y] > riverMin - riverInfluence && riverNoiseMap[x, y] < riverMax + riverInfluence)
                {
                    heightMap[x, y] = 0.16f;
                }
                else if(riverNoiseMap[x, y] > riverMin - 2 * riverInfluence && riverNoiseMap[x, y] < riverMax + 2 * riverInfluence)
                {
                    heightMap[x, y] = 0.25f;
                }
                else if(riverNoiseMap[x, y] > riverMin - 3 * riverInfluence && riverNoiseMap[x, y] < riverMax + 3 * riverInfluence)
                {
                    heightMap[x, y] = 0.3f;
                }
            }
        }
    }
}
