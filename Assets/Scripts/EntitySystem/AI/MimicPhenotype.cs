using Unity.Mathematics;

public struct MimicPhenotype
{
    public int NumberOfLegs, PartsPerLeg, MinimumAnchoredLegs, LegResolution, VerticeCount;
    public float MinLegLifetime, MaxLegLifetime, NewLegRadius, MinLegDistance;
    public float MinGrowCoef, MaxGrowCoef, NewLegCooldown;
    public float LegMinHeight, LegMaxHeight;
    public float HandleOffsetMinRadius, HandleOffsetMaxRadius;
    public float MinRotSpeed, MaxRotSpeed, MinOscillationSpeed, MaxOscillationSpeed;
    public float LegWidth;

    public static MimicPhenotype FromGenetics(GeneticParameters g)
    {
        var rng = new XorShift32(g.ComputeHash());

        float exploration    = Normalize(g.EntropyRegularization, GeneticParameters.ParameterRanges[GeneticParameters.IdxEntropyRegularization]);
        float mutability     = Normalize(g.MutationChance,        GeneticParameters.ParameterRanges[GeneticParameters.IdxMutationChance]);
        float temperature    = Normalize(g.SoftmaxTemperature,    GeneticParameters.ParameterRanges[GeneticParameters.IdxSoftmaxTemperature]);
        float noise          = Normalize(g.GaussianNoise,         GeneticParameters.ParameterRanges[GeneticParameters.IdxGaussianNoise]);
        float learningRate   = Normalize(g.BaseLearningRate,      GeneticParameters.ParameterRanges[GeneticParameters.IdxBaseLearningRate]);
        float speed          = Normalize(g.MoveSpeedMul,          GeneticParameters.ParameterRanges[GeneticParameters.IdxMoveSpeedMul]);
        float gradientNorm   = Normalize(g.MaxGradientNorm,       GeneticParameters.ParameterRanges[GeneticParameters.IdxMaxGradientNorm]);
        float capacity       = Normalize(g.MaxEnergyMul,          GeneticParameters.ParameterRanges[GeneticParameters.IdxMaxEnergyMul]);
        float entropyAlpha   = Normalize(g.EntropyAlpha,          GeneticParameters.ParameterRanges[GeneticParameters.IdxEntropyAlpha]);
        float detection      = Normalize(g.DetectionRadiusMul,    GeneticParameters.ParameterRanges[GeneticParameters.IdxDetectionRadiusMul]);


        return new MimicPhenotype
        {
            NumberOfLegs          = rng.Range(3, 6) + (int)math.round(exploration * 3f),
            PartsPerLeg           = rng.Range(2, 4) + (int)math.round(gradientNorm * 2f),
            MinimumAnchoredLegs   = math.max(1, rng.Range(1, 3)),
            LegResolution         = 20 + rng.Range(0, 20) + (int)math.round(capacity * 20f),
            VerticeCount          = rng.Range(4, 9),

            MinLegLifetime        = math.lerp(3f, 8f, 1f - speed),
            MaxLegLifetime        = math.lerp(8f, 20f, 1f - speed),
            NewLegRadius          = math.lerp(2f, 5f, gradientNorm),
            MinLegDistance        = math.lerp(3f, 6f, 1f - exploration),
            MinGrowCoef           = math.lerp(3f, 6f, learningRate),
            MaxGrowCoef           = math.lerp(6f, 10f, learningRate),
            NewLegCooldown        = math.lerp(0.1f, 0.6f, 1f - temperature),
            LegMinHeight          = math.lerp(0.5f, 1.5f, detection),
            LegMaxHeight          = math.lerp(1.5f, 4f, detection),
            HandleOffsetMinRadius = math.lerp(0.2f, 0.8f, noise),
            HandleOffsetMaxRadius = math.lerp(0.8f, 2.2f, noise),
            MinRotSpeed           = math.lerp(5f, 20f, temperature),
            MaxRotSpeed           = math.lerp(20f, 45f, temperature),
            MinOscillationSpeed   = math.lerp(0.5f, 2f, entropyAlpha),
            MaxOscillationSpeed   = math.lerp(2f, 5f, entropyAlpha),
            LegWidth              = math.lerp(0.1f, 0.4f, mutability),
        };
    }

    public void ApplyTo(Mimic mimic)
    {
        mimic.numberOfLegs = NumberOfLegs;
        mimic.partsPerLeg = PartsPerLeg;
        mimic.minimumAnchoredLegs = MinimumAnchoredLegs;
        mimic.legResolution = LegResolution;
        mimic.verticeCount = VerticeCount;
        mimic.minLegLifetime = MinLegLifetime;
        mimic.maxLegLifetime = MaxLegLifetime;
        mimic.newLegRadius = NewLegRadius;
        mimic.minLegDistance = MinLegDistance;
        mimic.minGrowCoef = MinGrowCoef;
        mimic.maxGrowCoef = MaxGrowCoef;
        mimic.newLegCooldown = NewLegCooldown;
        mimic.legMinHeight = LegMinHeight;
        mimic.legMaxHeight = LegMaxHeight;
        mimic.handleOffsetMinRadius = HandleOffsetMinRadius;
        mimic.handleOffsetMaxRadius = HandleOffsetMaxRadius;
        mimic.minRotSpeed = MinRotSpeed;
        mimic.maxRotSpeed = MaxRotSpeed;
        mimic.minOscillationSpeed = MinOscillationSpeed;
        mimic.maxOscillationSpeed = MaxOscillationSpeed;
        mimic.legWidth = LegWidth;
    }

    private static float Normalize(float value, (float min, float max, float mutationScale) range)
        => math.saturate((value - range.min) / (range.max - range.min));
}