using Unity.Mathematics;

public struct ColonyGenome
{
    public float BaseLearningRate;
    public float MaxGradientNorm;
    public float EntropyRegularization;

    public static ColonyGenome Initial(float spread)
    {
        spread = math.saturate(spread);
        var g = default(ColonyGenome);
        g.BaseLearningRate      = Start(GeneticParameters.IdxBaseLearningRate, spread);
        g.MaxGradientNorm       = Start(GeneticParameters.IdxMaxGradientNorm, spread);
        g.EntropyRegularization = Start(GeneticParameters.IdxEntropyRegularization, spread);
        return g;
    }

    public ColonyGenome Inherited(float chance)
    {
        var g = this;
        g.BaseLearningRate      = Mutate(g.BaseLearningRate, GeneticParameters.IdxBaseLearningRate, chance);
        g.MaxGradientNorm       = Mutate(g.MaxGradientNorm, GeneticParameters.IdxMaxGradientNorm, chance);
        g.EntropyRegularization = Mutate(g.EntropyRegularization, GeneticParameters.IdxEntropyRegularization, chance);
        return g;
    }

    private static float Start(int index, float spread)
    {
        var (min, max, _) = GeneticParameters.ParameterRanges[index];
        float mid = 0.5f * (min + max);
        if (spread <= 0f) return mid;
        return math.lerp(mid, RavineRandom.RangeFloat(min, max), spread);
    }

    private static float Mutate(float value, int index, float chance)
    {
        if (RavineRandom.RangeFloat() >= chance) return value;
        var (min, max, scale) = GeneticParameters.ParameterRanges[index];
        return math.clamp(value + RavineRandom.RangeFloat(-1f, 1f) * scale, min, max);
    }
}
