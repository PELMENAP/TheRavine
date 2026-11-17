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

        public readonly static NoiseLayerSettings DefaultHeight = new()
        {
            noiseType = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType = FastNoiseLite.FractalType.FBm,
            frequency = 0.005f,
            octaves = 4,
            lacunarity = 2f,
            gain = 0.5f,
            weightedStrength = 0f
        };

        public readonly static NoiseLayerSettings DefaultRiver = new()
        {
            noiseType = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType = FastNoiseLite.FractalType.FBm,
            frequency = 0.0025f,
            octaves = 3,
            lacunarity = 2f,
            gain = 0.5f,
            weightedStrength = 0f
        };

        public readonly static NoiseLayerSettings DefaultTemperature = new()
        {
            noiseType        = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType      = FastNoiseLite.FractalType.FBm,
            frequency        = 0.002f,
            octaves          = 2,
            lacunarity       = 2f,
            gain             = 0.30f,
            weightedStrength = 0f
        };

        public readonly static NoiseLayerSettings DefaultMoisture = new()
        {
            noiseType        = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType      = FastNoiseLite.FractalType.FBm,
            frequency        = 0.003f,
            octaves          = 3,
            lacunarity       = 2f,
            gain             = 0.45f,
            weightedStrength = 0.1f
        };

        public readonly static NoiseLayerSettings DefaultContinentalness = new()
        {
            noiseType        = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType      = FastNoiseLite.FractalType.FBm,
            frequency        = 0.0007f,
            octaves          = 2,
            lacunarity       = 2f,
            gain             = 0.5f,
            weightedStrength = 0f
        };

        public readonly static NoiseLayerSettings DefaultRuggedness = new()
        {
            noiseType        = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType      = FastNoiseLite.FractalType.FBm,
            frequency        = 0.0011f,
            octaves          = 2,
            lacunarity       = 2f,
            gain             = 0.5f,
            weightedStrength = 0f
        };

        public readonly static NoiseLayerSettings DefaultRidge = new()
        {
            noiseType        = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType      = FastNoiseLite.FractalType.Ridged,
            frequency        = 0.004f,
            octaves          = 4,
            lacunarity       = 2f,
            gain             = 0.5f,
            weightedStrength = 0f
        };

        public readonly static NoiseLayerSettings DefaultLandformWarp = new()
        {
            noiseType        = FastNoiseLite.NoiseType.OpenSimplex2,
            fractalType      = FastNoiseLite.FractalType.None,
            frequency        = 0.012f,
            octaves          = 1,
            lacunarity       = 2f,
            gain             = 0.5f,
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

        public readonly static RiverBlendSettings Default = new()
        {
            riverMin = 0.45f,
            riverMax = 0.60f,
            influenceWidth = 0.15f,
            riverBedHeight = 0.05f
        };
    }


    [Serializable]
    public struct BiomeSettings
    {
        public string name;

        [Range(0f, 1f)] public float minTemperature, maxTemperature;
        [Range(0f, 1f)] public float minMoisture,    maxMoisture;

        public float heightScale;
        public float heightOffset;

        public bool              hasRivers;
        public RiverBlendSettings riverBlend;

        [Min(0)] public int albedoLayer;
        public Color tint;
        public Color grassTint;
        public readonly Vector2 Center => new(
            (minTemperature + maxTemperature) * 0.5f,
            (minMoisture    + maxMoisture)    * 0.5f);
            
    }

    [Serializable]
    public struct LandformSettings
    {
        public string name;

        [Range(0f, 1f)] public float continentalness;
        [Range(0f, 1f)] public float ruggedness;

        public float heightScale;
        public float heightOffset;
        [Range(0f, 1f)] public float ridgeWeight;
        [Min(0f)] public float warpAmplitude;
        [Range(0f, 1f)] public float terraceWeight;
        [Min(0f)] public float terraceStep;

        [Min(0f)] public float riverWidth;
        [Range(0f, 1f)] public float riverCarve;
        [Min(0f)] public float riverWarp;

        [Min(0f)] public float erosion;
    }
}
