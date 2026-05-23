using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [Serializable]
    public struct NoiseLayerSettings
    {
        public FastNoiseLite.NoiseType noiseType;
        public FastNoiseLite.FractalType fractalType;
        [Range(0.0001f, 0.1f)] public float frequency;
        [Range(1, 8)] public int octaves;
        [Range(1f, 4f)] public float lacunarity;
        [Range(0f, 1f)] public float gain;
        [Range(0f, 1f)] public float weightedStrength;

        public static NoiseLayerSettings DefaultHeight => new()
        {
            noiseType = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType = FastNoiseLite.FractalType.FBm,
            frequency = 0.005f,
            octaves = 4,
            lacunarity = 2f,
            gain = 0.5f,
            weightedStrength = 0f
        };

        public static NoiseLayerSettings DefaultRiver => new()
        {
            noiseType = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType = FastNoiseLite.FractalType.FBm,
            frequency = 0.0025f,
            octaves = 3,
            lacunarity = 2f,
            gain = 0.5f,
            weightedStrength = 0f
        };
    }

    [Serializable]
    public struct RiverBlendSettings
    {
        [Range(0f, 1f)] public float riverMin;
        [Range(0f, 1f)] public float riverMax;
        [Range(0f, 0.3f)] public float influenceWidth;
        [Range(0f, 0.3f)] public float riverBedHeight;
        [Range(0f, 0.5f)] public float minTerrainHeight;
        public bool useDomainWarp;
        [Range(0f, 50f)] public float domainWarpAmplitude;

        public static RiverBlendSettings Default => new()
        {
            riverMin = 0.45f,
            riverMax = 0.60f,
            influenceWidth = 0.15f,
            riverBedHeight = 0.05f,
            minTerrainHeight = 0.20f,
            useDomainWarp = false,
            domainWarpAmplitude = 20f
        };
    }
}