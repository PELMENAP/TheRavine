using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [Serializable]
    public struct TerrainType
    {
        public float height;
    }


    [Serializable]
    public class ChunkGenerationSettings
    {
        public bool isRiver;
        public bool[] endlessFlag;

        public NoiseLayerSettings heightNoiseSettings = NoiseLayerSettings.DefaultHeight;
        public NoiseLayerSettings riverNoiseSettings  = NoiseLayerSettings.DefaultRiver;
        public NoiseLayerSettings temperatureSettings = NoiseLayerSettings.DefaultTemperature;
        public NoiseLayerSettings moistureSettings    = NoiseLayerSettings.DefaultMoisture;

        public int mountainRegionIndex = 8;

        public TerrainType[] regions;
        public BiomeSettings[] biomesSettings = BiomePresets.All;
        public float biomeBlendRadius = 0.25f;
        public float altitudeCooling = 0.35f;

        public ErosionSettings erosion;
        public ObjectSpawnProfileSO[] spawnProfiles;
        public int maxObjectsPerChunk = 2048;

        [Header("Streaming")]
        [Min(0.5f)] public float frameBudgetMs = 4f;
        [Min(0)] public int maxLodRing = 4;
        public int[] lodSteps = { 4, 8, 16, 32 };
        [Min(0f)] public float skirtDepth = 1.5f;

        [Header("Chunk Lifecycle")]
        [Min(2)] public int retainRadius = 3;
        [Min(0)] public int evictionHysteresis = 2;
        [Min(0)] public int evictionIdleFrames = 120;
        [Min(0.1f)] public float evictionScanInterval = 5f;
        [Min(0)] public int chunkPoolCapacity = 64;
    }
}
