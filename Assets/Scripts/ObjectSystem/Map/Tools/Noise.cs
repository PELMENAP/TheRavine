using UnityEngine;
using TheRavine.Generator;

public static class Noise
{
    private static FastNoiseLite _heightNoise;
    private static FastNoiseLite _riverNoise;
    private static FastNoiseLite _domainWarp;

    private static int _chunkSize;

    public static void SetInit(
        NoiseLayerSettings heightSettings,
        NoiseLayerSettings riverSettings,
        int seed,
        int chunkSize)
    {
        _chunkSize = chunkSize;
        _heightNoise = BuildNoise(heightSettings, seed);
        _riverNoise = BuildNoise(riverSettings, seed * 2);

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

    public static void GenerateHeightMap(ref float[,] noiseMap, Vector2Int chunkOffset)
    {
        FillMap(ref noiseMap, _heightNoise, chunkOffset);
    }

    public static void GenerateRiverMap(ref float[,] noiseMap, Vector2Int chunkOffset)
    {
        FillMap(ref noiseMap, _riverNoise, chunkOffset);
    }

    // FastNoiseLite FBm output is bounded to -1..1 — remap once to 0..1.
    // No need for two-pass local normalization or manual octave accumulation.
    private static void FillMap(ref float[,] map, FastNoiseLite noise, Vector2Int chunkOffset)
    {
        int worldX = chunkOffset.x * _chunkSize;
        int worldY = chunkOffset.y * _chunkSize;

        for (int y = 0; y < _chunkSize; y++)
        {
            int wy = worldY + y;
            for (int x = 0; x < _chunkSize; x++)
            {
                map[x, y] = noise.GetNoise(worldX + x, wy) * 0.5f + 0.5f;
            }
        }
    }

    // Smooth river blend using SmoothStep distance falloff.
    //
    // Algorithm:
    //   1. Compute signed distance from river centerline in noise-value space.
    //   2. Normalize by total influence radius → t ∈ [0,1], where 1 = river center.
    //   3. Apply smoothstep (Hermite) to get C1-continuous transition.
    //   4. Lerp heightMap toward riverBedHeight by t.
    //
    // Compared to hardcoded stepped bands this gives:
    //   - One set of intuitive parameters instead of 4 magic floats
    //   - C1-continuous, no visible seam lines between bands
    //   - Optional Domain Warp on river coordinates for organic meanders
    //
    public static void CombineMaps(
        ref float[,] heightMap,
        float[,] riverMap,
        in RiverBlendSettings s)
    {
        float riverCenter = (s.riverMin + s.riverMax) * 0.5f;
        float totalRadius = (s.riverMax - s.riverMin) * 0.5f + s.influenceWidth;
        float rcpRadius = 1f / totalRadius;

        for (int y = 0; y < _chunkSize; y++)
        {
            for (int x = 0; x < _chunkSize; x++)
            {
                float terrainH = heightMap[x, y];
                if (terrainH < s.minTerrainHeight) continue;

                float rv = riverMap[x, y];

                if (s.useDomainWarp)
                {
                    float wx = x, wy = y;
                    _domainWarp.SetDomainWarpAmp(s.domainWarpAmplitude);
                    _domainWarp.DomainWarp(ref wx, ref wy);
                    rv = _riverNoise.GetNoise(wx, wy) * 0.5f + 0.5f;
                }

                float dist = Mathf.Abs(rv - riverCenter);
                if (dist >= totalRadius) continue;

                float t = 1f - dist * rcpRadius;
                t = t * t * (3f - 2f * t);
                heightMap[x, y] = Mathf.LerpUnclamped(terrainH, s.riverBedHeight, t);
            }
        }
    }
}