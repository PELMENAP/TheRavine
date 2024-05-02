using UnityEngine;
using TheRavine.Generator;
using TheRavine.Extentions;
public static class Noise
{
    public enum NormalizeMode { Local, Global, Temp };
    private static Vector2[] octaveOffsets, tempOctaveOffsets;
    private static int octaves;
    private static float persistance, lacunarity, scaleInverse;
    private static float halfWidth = MapGenerator.mapChunkSize / 2f;
    private static float halfHeight = MapGenerator.mapChunkSize / 2f;
    public static void SetInit(float scale, byte _octaves, float _persistance, float _lacunarity, int seed)
    {
        FastRandom prng = new FastRandom(seed);
        FastRandom tprng = new FastRandom(seed * 2);
        octaveOffsets = new Vector2[_octaves];
        tempOctaveOffsets = new Vector2[_octaves];
        octaves = _octaves;
        persistance = _persistance;
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
            octaveOffsets[i] = new Vector2(offset.x + octaveOffsets[i].x, offset.y + octaveOffsets[i].y);
            maxPossibleHeight += amplitude;
            amplitude *= persistance;
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
                    float sampleX = (x + halfWidth + octaveOffsets[i].x) * scaleInverse * frequency;
                    float sampleY = (y + halfHeight + octaveOffsets[i].y) * scaleInverse * frequency;
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
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
                if (normalizeMode == NormalizeMode.Local)
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                else
                    noiseMap[x, y] = Mathf.Clamp((noiseMap[x, y] + 1) * globalNormalizeFactor, 0, int.MaxValue);
        for (byte i = 0; i < octaves; i++)
            octaveOffsets[i] = new Vector2(octaveOffsets[i].x - offset.x, octaveOffsets[i].y - offset.y);
    }

    public static void GenerateTempNoiseMap(ref float[,] noiseMap, Vector2 offset)
    {
        float maxPossibleHeight = 0;
        float amplitude = 1;

        for (byte i = 0; i < octaves; i++)
        {
            tempOctaveOffsets[i] = new Vector2(offset.x + tempOctaveOffsets[i].x, offset.y + tempOctaveOffsets[i].y);
            maxPossibleHeight += amplitude;
            amplitude *= persistance;
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
                    float sampleX = (x + halfWidth + tempOctaveOffsets[i].x) * 0.5f * scaleInverse * frequency;
                    float sampleY = (y + halfHeight + tempOctaveOffsets[i].y) * 0.5f * scaleInverse * frequency;
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
            for (byte x = 0; x < MapGenerator.mapChunkSize; x++)
                    noiseMap[x, y] = Mathf.Clamp((noiseMap[x, y] + 1) * globalNormalizeFactor, 0, int.MaxValue);
        for (byte i = 0; i < octaves; i++)
            tempOctaveOffsets[i] = new Vector2(tempOctaveOffsets[i].x - offset.x, tempOctaveOffsets[i].y - offset.y);
    }
}