using Unity.Mathematics;

public struct ColonyGenome
{
    public float BaseLearningRate;
    public float MaxGradientNorm;
    public float EntropyRegularization;

    public static ColonyGenome Random()
    {
        var g = default(ColonyGenome);
        g.BaseLearningRate      = Draw(GeneticParameters.IdxBaseLearningRate);
        g.MaxGradientNorm       = Draw(GeneticParameters.IdxMaxGradientNorm);
        g.EntropyRegularization = Draw(GeneticParameters.IdxEntropyRegularization);
        return g;
    }

    public static ColonyGenome FromAgent(in GeneticParameters p) => new ColonyGenome
    {
        BaseLearningRate      = p.BaseLearningRate,
        MaxGradientNorm       = p.MaxGradientNorm,
        EntropyRegularization = p.EntropyRegularization,
    };

    public ColonyGenome Inherited(float chance)
    {
        var g = this;
        g.BaseLearningRate      = Mutate(g.BaseLearningRate, GeneticParameters.IdxBaseLearningRate, chance);
        g.MaxGradientNorm       = Mutate(g.MaxGradientNorm, GeneticParameters.IdxMaxGradientNorm, chance);
        g.EntropyRegularization = Mutate(g.EntropyRegularization, GeneticParameters.IdxEntropyRegularization, chance);
        return g;
    }

    private static float Draw(int index)
    {
        var (min, max, _) = GeneticParameters.ParameterRanges[index];
        return RavineRandom.RangeFloat(min, max);
    }

    private static float Mutate(float value, int index, float chance)
    {
        if (RavineRandom.RangeFloat() >= chance) return value;
        var (min, max, scale) = GeneticParameters.ParameterRanges[index];
        return math.clamp(value + RavineRandom.RangeFloat(-1f, 1f) * scale, min, max);
    }
}
