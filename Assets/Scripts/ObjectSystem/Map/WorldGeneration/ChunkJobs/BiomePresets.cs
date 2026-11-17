using TheRavine.Generator;

// Координаты биомов в пространстве (temperature, moisture) — схема Whittaker:
//
//  moisture
//  1.0 │ Tundra  Taiga   TempRain  TropRain
//  0.7 │ Tundra  TempFor TempFor   TropFor
//  0.4 │ Steppe  Steppe  Savanna   Savanna
//  0.0 │ Tundra  Desert  Desert    Desert
//      └────────────────────────────────── temperature
//        0.0     0.25    0.55      0.85

public static class BiomePresets
{   
    private static RiverBlendSettings WideRiver => new()
    {
        riverMin          = 0.42f,
        riverMax          = 0.58f,
        influenceWidth    = 0.18f,
        riverBedHeight    = 0.04f
    };

    private static RiverBlendSettings NarrowRiver => new()
    {
        riverMin          = 0.47f,
        riverMax          = 0.53f,
        influenceWidth    = 0.08f,
        riverBedHeight    = 0.06f
    };

    private static RiverBlendSettings WadiRiver => new()
    {
        riverMin          = 0.48f,
        riverMax          = 0.52f,
        influenceWidth    = 0.05f,
        riverBedHeight    = 0.02f
    };

    private static RiverBlendSettings SwampRiver => new()
    {
        riverMin          = 0.38f,
        riverMax          = 0.62f,
        influenceWidth    = 0.22f,
        riverBedHeight    = 0.08f
    };

    public static BiomeSettings Tundra => new()
    {
        name             = "Tundra",
        minTemperature   = 0.00f,
        maxTemperature   = 0.25f,
        minMoisture      = 0.00f,
        maxMoisture      = 1.00f,
        heightScale      = 0.50f,
        heightOffset     = 0.18f,
        hasRivers        = false,
        riverBlend       = default
    };

    public static BiomeSettings BorealForest => new()
    {
        name             = "Boreal Forest (Taiga)",
        minTemperature   = 0.10f,
        maxTemperature   = 0.38f,
        minMoisture      = 0.35f,
        maxMoisture      = 0.75f,
        heightScale      = 0.75f,
        heightOffset     = 0.08f,
        hasRivers        = true,
        riverBlend       = NarrowRiver
    };

    public static BiomeSettings Steppe => new()
    {
        name             = "Temperate Grassland / Steppe",
        minTemperature   = 0.28f,
        maxTemperature   = 0.60f,
        minMoisture      = 0.00f,
        maxMoisture      = 0.38f,
        heightScale      = 0.65f,
        heightOffset     = 0.10f,
        hasRivers        = true,
        riverBlend       = NarrowRiver
    };

    public static BiomeSettings TemperateForest => new()
    {
        name             = "Temperate Deciduous Forest",
        minTemperature   = 0.30f,
        maxTemperature   = 0.62f,
        minMoisture      = 0.38f,
        maxMoisture      = 0.72f,
        heightScale      = 0.90f,
        heightOffset     = 0.06f,
        hasRivers        = true,
        riverBlend       = WideRiver
    };

    public static BiomeSettings TemperateRainforest => new()
    {
        name             = "Temperate Rainforest",
        minTemperature   = 0.22f,
        maxTemperature   = 0.52f,
        minMoisture      = 0.70f,
        maxMoisture      = 1.00f,
        heightScale      = 1.10f,
        heightOffset     = 0.04f,
        hasRivers        = true,
        riverBlend       = WideRiver
    };

    public static BiomeSettings Desert => new()
    {
        name             = "Hot Desert",
        minTemperature   = 0.62f,
        maxTemperature   = 1.00f,
        minMoisture      = 0.00f,
        maxMoisture      = 0.28f,
        heightScale      = 0.55f,
        heightOffset     = 0.12f,
        hasRivers        = true,
        riverBlend       = WadiRiver
    };

    public static BiomeSettings Savanna => new()
    {
        name             = "Savanna",
        minTemperature   = 0.60f,
        maxTemperature   = 0.92f,
        minMoisture      = 0.22f,
        maxMoisture      = 0.52f,
        heightScale      = 0.62f,
        heightOffset     = 0.09f,
        hasRivers        = true,
        riverBlend       = NarrowRiver
    };

    public static BiomeSettings TropicalForest => new()
    {
        name             = "Tropical Forest",
        minTemperature   = 0.72f,
        maxTemperature   = 1.00f,
        minMoisture      = 0.45f,
        maxMoisture      = 0.72f,
        heightScale      = 0.85f,
        heightOffset     = 0.07f,
        hasRivers        = true,
        riverBlend       = WideRiver
    };

    public static BiomeSettings TropicalRainforest => new()
    {
        name             = "Tropical Rainforest",
        minTemperature   = 0.78f,
        maxTemperature   = 1.00f,
        minMoisture      = 0.70f,
        maxMoisture      = 1.00f,
        heightScale      = 1.15f,
        heightOffset     = 0.05f,
        hasRivers        = true,
        riverBlend       = WideRiver
    };

    public static BiomeSettings Wetlands => new()
    {
        name             = "Wetlands / Swamp",
        minTemperature   = 0.38f,
        maxTemperature   = 0.78f,
        minMoisture      = 0.78f,
        maxMoisture      = 1.00f,
        heightScale      = 0.45f,
        heightOffset     = 0.05f,
        hasRivers        = true,
        riverBlend       = SwampRiver
    };

    public readonly static BiomeSettings[] All = new[]
    {
        Tundra,
        BorealForest,
        Steppe,
        TemperateForest,
        TemperateRainforest,
        Desert,
        Savanna,
        TropicalForest,
        TropicalRainforest,
        Wetlands
    };
}