public readonly struct AgentAudioState
{
    public readonly float BaseFrequency;
    public readonly float BaseVolume;
    public readonly float Brightness;
    public readonly float Stress;
    public readonly float AmplitudeNormDenom;

    public readonly WaveformType Primary;
    public readonly WaveformType Secondary;
    public readonly float WaveMorphAmount;

    public readonly float InharmonicityB;
    public readonly float PitchStartMul;
    public readonly float PitchDropTime;
    
    public readonly float VibratoFrequency;
    public readonly float VibratoDepthCents;
    public readonly float VibratoDelay;
    public readonly float VibratoRampTime;

    public readonly float DriftAmplitude;
    public readonly float DriftFrequency;
    public readonly float AmplitudeModDepth;
    public readonly float AmplitudeModFrequency;

    public readonly float FmRatio;
    public readonly float FmDepth;
    public readonly float RingModFrequency;

    public readonly float NoiseAmount; 
    public readonly float SaturationAmount; 
    public readonly float StressDecayMultiplier;

    public AgentAudioState(
        float baseFrequency, float baseVolume, float brightness, float stress, float amplitudeNormDenom,
        WaveformType primary, WaveformType secondary, float waveMorphAmount,
        float inharmonicityB, float pitchStartMul, float pitchDropTime,
        float vibratoFrequency, float vibratoDepthCents, float vibratoDelay, float vibratoRampTime,
        float driftAmplitude, float driftFrequency, float amplitudeModDepth, float amplitudeModFrequency,
        float fmRatio, float fmDepth, float ringModFrequency,
        float noiseAmount, float saturationAmount, float stressDecayMultiplier)
    {
        BaseFrequency = baseFrequency;
        BaseVolume = baseVolume;
        Brightness = brightness;
        Stress = stress;
        AmplitudeNormDenom = amplitudeNormDenom;
        Primary = primary;
        Secondary = secondary;
        WaveMorphAmount = waveMorphAmount;
        InharmonicityB = inharmonicityB;
        PitchStartMul = pitchStartMul;
        PitchDropTime = pitchDropTime;
        VibratoFrequency = vibratoFrequency;
        VibratoDepthCents = vibratoDepthCents;
        VibratoDelay = vibratoDelay;
        VibratoRampTime = vibratoRampTime;
        DriftAmplitude = driftAmplitude;
        DriftFrequency = driftFrequency;
        AmplitudeModDepth = amplitudeModDepth;
        AmplitudeModFrequency = amplitudeModFrequency;
        FmRatio = fmRatio;
        FmDepth = fmDepth;
        RingModFrequency = ringModFrequency;
        NoiseAmount = noiseAmount;
        SaturationAmount = saturationAmount;
        StressDecayMultiplier = stressDecayMultiplier;
    }
}