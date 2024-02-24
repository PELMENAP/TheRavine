using UnityEngine;
using TheRavine.Generator;
public static class Noise
{

    public enum NormalizeMode { Local, Global };
    // private static System.Random prng;
    private static Vector2[] octaveOffsets;
    private static int octaves;
    private static float persistance, lacunarity, scaleInverse;
    public static void SetInit(int seed, float scale, byte _octaves, float _persistance, float _lacunarity)
    {
        // prng = new System.Random(seed);
        octaveOffsets = new Vector2[_octaves];
        octaves = _octaves;
        persistance = _persistance;
        lacunarity = _lacunarity;
        scaleInverse = 1 / scale;
    }
    public static void GenerateNoiseMap(ref float[,] noiseMap, int seed, Vector2 offset, NormalizeMode normalizeMode)
    {
        System.Random prng = new System.Random(seed);

        float maxPossibleHeight = 0;
        float amplitude = 1;

        for (int i = 0; i < octaves; i++)
        {
            octaveOffsets[i] = new Vector2(prng.Next(-100000, 100000) - offset.x, prng.Next(-100000, 100000) - offset.y);
            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;
        float halfWidth = MapGenerator.mapChunkSize / 2f;
        float halfHeight = MapGenerator.mapChunkSize / 2f;

        for (byte y = 0; y < MapGenerator.mapChunkSize; y++)
        {
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
            {
                amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
                for (byte i = 0; i < octaves; i++)
                {
                    float sampleX = (x - halfWidth + octaveOffsets[i].x) * scaleInverse * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[i].y) * scaleInverse * frequency;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;
                    amplitude *= persistance;
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
        {
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
            {
                if (normalizeMode == NormalizeMode.Local)
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                else
                    noiseMap[x, y] = Mathf.Clamp((noiseMap[x, y] + 1) * globalNormalizeFactor, 0, int.MaxValue);
            }
        }
    }
}