public readonly struct TerrainSample
{
    public readonly float HeightNorm;
    public readonly float Slope;
    public readonly float GradX;
    public readonly float GradZ;
    public readonly float WaterProximity;
    public readonly float MoveCost;
    public readonly float MoveCostPX;
    public readonly float MoveCostNX;
    public readonly float MoveCostPZ;
    public readonly float MoveCostNZ;
    public readonly float Biome0;
    public readonly float Biome1;
    public readonly float Biome2;
    public readonly float Biome3;
    public readonly float Density2;
    public readonly float Density4;
    public readonly float Density8;
    public readonly float RelativeHeight;
    public readonly byte  Valid;

    public bool IsValid => Valid != 0;

    public TerrainSample(
        float heightNorm, float slope, float gradX, float gradZ,
        float waterProximity, float moveCost,
        float costPX, float costNX, float costPZ, float costNZ,
        float biome0, float biome1, float biome2, float biome3,
        float density2, float density4, float density8,
        float relativeHeight)
    {
        HeightNorm     = heightNorm;
        Slope          = slope;
        GradX          = gradX;
        GradZ          = gradZ;
        WaterProximity = waterProximity;
        MoveCost       = moveCost;
        MoveCostPX     = costPX;
        MoveCostNX     = costNX;
        MoveCostPZ     = costPZ;
        MoveCostNZ     = costNZ;
        Biome0         = biome0;
        Biome1         = biome1;
        Biome2         = biome2;
        Biome3         = biome3;
        Density2       = density2;
        Density4       = density4;
        Density8       = density8;
        RelativeHeight = relativeHeight;
        Valid          = 1;
    }
}