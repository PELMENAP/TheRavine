using Unity.Mathematics;

public static class AudioParameterMapper
{
    public static AgentAudioState BuildState(AgentGeneticAudioProfile genetic, in AgentAudioProfile profile)
    {
        var (primary, secondary, morph) = TimbreResolver.Resolve(
            profile.Harmonicity, profile.Aggression, profile.Stress);

        float normDenom = ComputeAmplitudeNormDenom(genetic.BaseHash.HarmonicAmplitudes, profile.Brightness);

        return new AgentAudioState(
            baseFrequency: profile.BaseFrequency * (1f + profile.Brightness * 0.15f - profile.Stress * 0.05f),
            baseVolume: profile.BaseVolume * (1f - profile.NoiseAmount * 0.3f),
            brightness: profile.Brightness,
            stress: profile.Stress,
            amplitudeNormDenom: normDenom,
            primary: primary,
            secondary: secondary,
            waveMorphAmount: morph,
            inharmonicityB: math.lerp(genetic.BaseHash.InharmonicityB, 0.003f, profile.Stress),
            pitchStartMul: math.lerp(1f, 2.5f, profile.Aggression),
            pitchDropTime: math.lerp(0.2f, 0.01f, profile.Stress),
            vibratoFrequency: genetic.BaseHash.VibratoFrequency,
            vibratoDepthCents: math.lerp(0f, 100f, profile.Sociality),
            vibratoDelay: genetic.BaseHash.VibratoDelay,
            driftAmplitude: math.lerp(0f, 4f, profile.Stress),
            amplitudeModDepth: math.lerp(0f, 0.4f, profile.NoiseAmount),
            fmRatio: genetic.BaseHash.FmRatio,
            fmDepth: math.lerp(0f, 6f, profile.Aggression),
            ringModFrequency: genetic.BaseHash.RingModFrequency
        );
    }

    private static float ComputeAmplitudeNormDenom(float[] baseAmplitudes, float brightness)
    {
        float sum = 0f;
        float inv = 1f / math.max(baseAmplitudes.Length - 1, 1);
        for (int i = 0; i < baseAmplitudes.Length; i++)
            sum += baseAmplitudes[i] * math.lerp(1f, 1f + brightness * 0.5f, i * inv);
        return sum > 1e-6f ? sum : 1f;
    }
}