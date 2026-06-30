using Unity.Mathematics;

public static class TimbreResolver
{
    public static (WaveformType primary, WaveformType secondary, float morph) Resolve(
        float harmonicity, float aggression, float stress)
    {
        WaveformType primary = harmonicity > 0.5f ? WaveformType.Sine : WaveformType.Triangle;
        WaveformType secondary = aggression > stress ? WaveformType.Square : WaveformType.Saw;
        float morph = math.saturate(aggression * 0.6f + stress * 0.4f);
        return (primary, secondary, morph);
    }
}