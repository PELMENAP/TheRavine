using UnityEngine;

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

        float exploration    = Normalize(g.ExplorationPrice, GeneticParameters.ParameterRanges[10]);
        float mutability     = Normalize(g.MutationChance, GeneticParameters.ParameterRanges[11]);
        float temperature    = Normalize(g.SoftmaxTemperature, GeneticParameters.ParameterRanges[4]);
        float noise          = Normalize(g.GaussianNoise, GeneticParameters.ParameterRanges[9]);
        float learningRate   = Normalize(g.BaseLearningRate, GeneticParameters.ParameterRanges[2]);
        float lambda         = Normalize(g.Lambda, GeneticParameters.ParameterRanges[1]);
        float gradientNorm   = Normalize(g.MaxGradientNorm, GeneticParameters.ParameterRanges[3]);
        float labelSmoothing = Normalize(g.LabelSmoothing, GeneticParameters.ParameterRanges[6]);
        float entropyAlpha   = Normalize(g.EntropyAlpha, GeneticParameters.ParameterRanges[7]);
        float initBias       = Normalize(g.InitBiasesValues, GeneticParameters.ParameterRanges[8]);

        return new MimicPhenotype
        {
            NumberOfLegs          = rng.Range(3, 6) + Mathf.RoundToInt(exploration * 3f),
            PartsPerLeg           = rng.Range(2, 4) + Mathf.RoundToInt(gradientNorm * 2f),
            MinimumAnchoredLegs   = Mathf.Max(1, rng.Range(1, 3)),
            LegResolution         = 20 + rng.Range(0, 20) + Mathf.RoundToInt(labelSmoothing * 20f),
            VerticeCount          = rng.Range(4, 9),

            MinLegLifetime        = Mathf.Lerp(3f, 8f, 1f - lambda),
            MaxLegLifetime        = Mathf.Lerp(8f, 20f, 1f - lambda),
            NewLegRadius          = Mathf.Lerp(2f, 5f, gradientNorm),
            MinLegDistance        = Mathf.Lerp(3f, 6f, 1f - exploration),
            MinGrowCoef           = Mathf.Lerp(3f, 6f, learningRate),
            MaxGrowCoef           = Mathf.Lerp(6f, 10f, learningRate),
            NewLegCooldown        = Mathf.Lerp(0.1f, 0.6f, 1f - temperature),
            LegMinHeight          = Mathf.Lerp(0.5f, 1.5f, initBias),
            LegMaxHeight          = Mathf.Lerp(1.5f, 4f, initBias),
            HandleOffsetMinRadius = Mathf.Lerp(0.2f, 0.8f, noise),
            HandleOffsetMaxRadius = Mathf.Lerp(0.8f, 2.2f, noise),
            MinRotSpeed           = Mathf.Lerp(5f, 20f, temperature),
            MaxRotSpeed           = Mathf.Lerp(20f, 45f, temperature),
            MinOscillationSpeed   = Mathf.Lerp(0.5f, 2f, entropyAlpha),
            MaxOscillationSpeed   = Mathf.Lerp(2f, 5f, entropyAlpha),
            LegWidth              = Mathf.Lerp(0.1f, 0.4f, mutability),
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
        => Mathf.Clamp01((value - range.min) / (range.max - range.min));
}