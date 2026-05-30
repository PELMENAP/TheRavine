using System;

namespace TheRavine.Generator
{
    [Serializable]
    public struct TerrainType
    {
        public float height;
        public bool liveAble;
        public TemperatureLevel[] level;
    }

    [Serializable]
    public struct TemperatureLevel
    {
        public ObjectInfoGeneration[] objects;
        public StructInfoGeneration[] structs;
    }

    [Serializable]
    public struct ObjectInfoGeneration
    {
        public int Chance;
        public ObjectInfo info;
    }

    [Serializable]
    public struct StructInfoGeneration
    {
        public bool isSpawnPoint;
        public int Chance;
        public GenerationSettingsSO Settings;
        public TilePatternSO Pattern;
    }

    [Serializable]
    public struct TemperatureType
    {
        public float height;
    }

    [Serializable]
    public class ChunkGenerationSettings
    {
        public int rareness, seed, farlands;
        public bool isRiver;
        public bool[] endlessFlag;

        public NoiseLayerSettings heightNoiseSettings = NoiseLayerSettings.DefaultHeight;
        public NoiseLayerSettings riverNoiseSettings  = NoiseLayerSettings.DefaultRiver;
        public NoiseLayerSettings temperatureSettings = NoiseLayerSettings.DefaultTemperature;
        public NoiseLayerSettings moistureSettings    = NoiseLayerSettings.DefaultMoisture;
        public RiverBlendSettings riverBlend          = RiverBlendSettings.Default;

        public int mountainRegionIndex = 8;

        public TerrainType[] regions;
        public BiomeSettings[] biomesSettings = BiomePresets.All;
        public float biomeBlendRadius = 0.25f;
        public float altitudeCooling = 0.35f;

        public ErosionSettings erosion;
    }
}