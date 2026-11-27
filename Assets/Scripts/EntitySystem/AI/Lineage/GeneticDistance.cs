using Unity.Mathematics;

public static class GeneticDistance
{
    public static float Between(in GeneticParameters a, in GeneticParameters b)
    {
        var sa = GeneticParameters.AsReadOnlySpan(in a);
        var sb = GeneticParameters.AsReadOnlySpan(in b);
        var ranges = GeneticParameters.ParameterRanges;
        float sum = 0f;
        for (int i = 0; i < sa.Length; i++)
        {
            float width = ranges[i].max - ranges[i].min;
            if (!(width > 0f)) continue;
            float d = (sa[i] - sb[i]) / width;
            sum += d * d;
        }
        return math.saturate(math.sqrt(sum / math.max(sa.Length, 1)));
    }
}
