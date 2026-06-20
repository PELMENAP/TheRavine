using System;

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
    }
}