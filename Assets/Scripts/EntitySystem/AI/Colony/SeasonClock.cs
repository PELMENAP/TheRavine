using Unity.Mathematics;

public static class SeasonClock
{
    public static float Value(float time)
    {
        ref readonly var r = ref SimulationRules.Frame;
        return math.sin(2f * math.PI * time / math.max(r.SeasonPeriod, 1f));
    }

    public static bool IsWinter(float time) => Value(time) < -SimulationRules.Frame.WinterThreshold;

    public static float WinterApproach(float time)
    {
        ref readonly var r = ref SimulationRules.Frame;
        float phase = math.frac(time / math.max(r.SeasonPeriod, 1f));
        float a     = math.asin(math.saturate(r.WinterThreshold)) / (2f * math.PI);
        float start = 0.5f + a;
        float end   = 1f - a;
        if (phase >= start && phase <= end) return 1f;
        float lead = math.max(r.PreWinterFraction, 1e-3f);
        float to   = start - phase;
        return to > 0f && to < lead ? 1f - to / lead : 0f;
    }
}
