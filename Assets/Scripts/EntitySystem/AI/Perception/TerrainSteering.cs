using Unity.Mathematics;

public static class TerrainSteering
{
    public static float2 CostGradient(in TerrainSample t)
        => new float2(t.MoveCostNX - t.MoveCostPX, t.MoveCostNZ - t.MoveCostPZ);

    public static float2 Bias(in TerrainSample t)
    {
        ref readonly var r = ref SimulationRules.Frame;

        float2 slope = new float2(t.GradX, t.GradZ);

        float2 bias = CostGradient(in t) * r.TerrainCostWeight
                    - slope * (r.TerrainSlopeWeight * t.Slope)
                    + slope * (r.TerrainWaterWeight * t.WaterProximity);

        return math.normalizesafe(bias, float2.zero);
    }

    public static float2 Blend(in float2 desired, in TerrainSample t)
    {
        float2 d = math.normalizesafe(desired, new float2(1f, 0f));
        if (!t.IsValid) return d;

        float w = SimulationRules.Frame.TerrainBiasWeight;
        if (w <= 0f) return d;

        return math.normalizesafe(math.lerp(d, Bias(in t), math.saturate(w)), d);
    }
}