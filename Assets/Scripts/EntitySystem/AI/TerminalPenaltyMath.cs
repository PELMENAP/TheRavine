using Unity.Mathematics;

public static class TerminalPenaltyMath
{
    public static float Scale(float z, float sensitivity, float minScale, float maxScale)
    {
        float lo = math.max(minScale, 0f);
        return math.clamp(1f - z * sensitivity, lo, math.max(lo, maxScale));
    }

    public static float Compute(float basePenalty, bool statsReady, float z, float sensitivity, float minScale, float maxScale)
        => statsReady ? basePenalty * Scale(z, sensitivity, minScale, maxScale) : basePenalty;

    public static float For(SimulationRules rules, DeathCause cause, bool statsReady, float z)
        => Compute(rules.TerminalPenalty * rules.DeathPenaltyMul(cause), statsReady, z,
            rules.TerminalFitnessSensitivity, rules.TerminalPenaltyMinScale, rules.TerminalPenaltyMaxScale);
}
