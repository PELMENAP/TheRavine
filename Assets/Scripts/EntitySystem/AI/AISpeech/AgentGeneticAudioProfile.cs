using System;
using Unity.Collections;

public sealed class AgentGeneticAudioProfile : IDisposable
{
    public readonly StableHashService.HashData BaseHash;
    public readonly NativeArray<HarmonicPreset> Harmonics;
    public readonly int HarmonicsCount;

    public AgentGeneticAudioProfile(string geneticTimbreSeed, int harmonicsCount, float duration)
    {
        HarmonicsCount = harmonicsCount;
        BaseHash = StableHashService.GetOrCreate(geneticTimbreSeed, harmonicsCount, duration);

        Harmonics = new NativeArray<HarmonicPreset>(harmonicsCount, Allocator.Persistent);
        for (int i = 0; i < harmonicsCount; i++)
        {
            Harmonics[i] = new HarmonicPreset
            {
                FreqMul = BaseHash.HarmonicFreqMuls[i],
                Amplitude = BaseHash.HarmonicAmplitudes[i],
                Phase = BaseHash.HarmonicPhases[i],
                DecayRate = BaseHash.HarmonicDecayRates[i],
                DriftPhase = BaseHash.HarmonicDriftPhases[i]
            };
        }
    }

    public void Dispose()
    {
        if (Harmonics.IsCreated) Harmonics.Dispose();
    }
}