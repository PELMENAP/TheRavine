using System;
using Unity.Collections;
using System.Threading;

public sealed class AgentGeneticAudioProfile : IDisposable
{
    public readonly StableHashService.HashData BaseHash;
    public readonly NativeArray<HarmonicPreset> Harmonics;
    public readonly int HarmonicsCount;

    private int refCount;
    private bool pendingDispose;

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

    public void Acquire() => Interlocked.Increment(ref refCount);

    public void Release()
    {
        if (Interlocked.Decrement(ref refCount) == 0 && pendingDispose)
            DisposeInternal();
    }

    public void RequestDispose()
    {
        pendingDispose = true;
        if (refCount == 0) DisposeInternal();
    }

    private void DisposeInternal()
    {
        if (Harmonics.IsCreated) Harmonics.Dispose();
    }

    public void Dispose() => RequestDispose();
}