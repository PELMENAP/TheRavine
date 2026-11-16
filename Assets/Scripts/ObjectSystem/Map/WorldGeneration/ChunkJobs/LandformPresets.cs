using TheRavine.Generator;

public static class LandformPresets
{
    public static LandformSettings Hills => new()
    {
        name             = "Hills",
        continentalness  = 0.55f,
        ruggedness       = 0.5f,
        heightScale      = 1.0f,
        heightOffset     = 0.0f,
        ridgeWeight      = 0.0f,
        warpAmplitude    = 0.0f,
        terraceWeight    = 0.0f,
        terraceStep      = 0.0f,
        riverWidth       = 1.0f,
        riverCarve       = 1.0f,
        riverWarp        = 0.0f,
        erosion          = 1.0f
    };

    public static LandformSettings Ocean => new()
    {
        name             = "Ocean",
        continentalness  = 0.1f,
        ruggedness       = 0.25f,
        heightScale      = 0.35f,
        heightOffset     = -0.7f,
        ridgeWeight      = 0.0f,
        warpAmplitude    = 0.0f,
        terraceWeight    = 0.0f,
        terraceStep      = 0.0f,
        riverWidth       = 0.0f,
        riverCarve       = 0.0f,
        riverWarp        = 0.0f,
        erosion          = 0.2f
    };

    public static LandformSettings Archipelago => new()
    {
        name             = "Archipelago",
        continentalness  = 0.15f,
        ruggedness       = 0.8f,
        heightScale      = 0.9f,
        heightOffset     = -0.55f,
        ridgeWeight      = 0.5f,
        warpAmplitude    = 30.0f,
        terraceWeight    = 0.0f,
        terraceStep      = 0.0f,
        riverWidth       = 0.5f,
        riverCarve       = 0.5f,
        riverWarp        = 0.0f,
        erosion          = 0.6f
    };

    public static LandformSettings Floodplain => new()
    {
        name             = "Floodplain",
        continentalness  = 0.4f,
        ruggedness       = 0.12f,
        heightScale      = 0.25f,
        heightOffset     = -0.05f,
        ridgeWeight      = 0.0f,
        warpAmplitude    = 0.0f,
        terraceWeight    = 0.0f,
        terraceStep      = 0.0f,
        riverWidth       = 1.8f,
        riverCarve       = 1.0f,
        riverWarp        = 40.0f,
        erosion          = 0.5f
    };

    public static LandformSettings Plains => new()
    {
        name             = "Plains",
        continentalness  = 0.6f,
        ruggedness       = 0.2f,
        heightScale      = 0.45f,
        heightOffset     = 0.05f,
        ridgeWeight      = 0.0f,
        warpAmplitude    = 10.0f,
        terraceWeight    = 0.0f,
        terraceStep      = 0.0f,
        riverWidth       = 1.2f,
        riverCarve       = 1.0f,
        riverWarp        = 20.0f,
        erosion          = 0.8f
    };

    public static LandformSettings Plateau => new()
    {
        name             = "Plateau",
        continentalness  = 0.85f,
        ruggedness       = 0.3f,
        heightScale      = 0.6f,
        heightOffset     = 0.45f,
        ridgeWeight      = 0.1f,
        warpAmplitude    = 15.0f,
        terraceWeight    = 0.8f,
        terraceStep      = 0.12f,
        riverWidth       = 0.6f,
        riverCarve       = 1.0f,
        riverWarp        = 10.0f,
        erosion          = 1.2f
    };

    public static LandformSettings Badlands => new()
    {
        name             = "Badlands",
        continentalness  = 0.5f,
        ruggedness       = 0.85f,
        heightScale      = 1.0f,
        heightOffset     = 0.1f,
        ridgeWeight      = 0.35f,
        warpAmplitude    = 45.0f,
        terraceWeight    = 0.5f,
        terraceStep      = 0.08f,
        riverWidth       = 0.7f,
        riverCarve       = 1.0f,
        riverWarp        = 25.0f,
        erosion          = 2.0f
    };

    public static LandformSettings Mountains => new()
    {
        name             = "Mountains",
        continentalness  = 0.9f,
        ruggedness       = 0.85f,
        heightScale      = 1.7f,
        heightOffset     = 0.35f,
        ridgeWeight      = 0.8f,
        warpAmplitude    = 25.0f,
        terraceWeight    = 0.0f,
        terraceStep      = 0.0f,
        riverWidth       = 0.5f,
        riverCarve       = 0.6f,
        riverWarp        = 5.0f,
        erosion          = 1.5f
    };

    public readonly static LandformSettings[] All = new[]
    {
        Hills,
        Ocean,
        Archipelago,
        Floodplain,
        Plains,
        Plateau,
        Badlands,
        Mountains
    };
}
