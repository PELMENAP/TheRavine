using System.Buffers;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


public static class AudioSynthesizer
{
    [BurstCompile]
    private struct SynthJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> samples;
        [ReadOnly] public NativeArray<float> harmonics;
        [ReadOnly] public NativeArray<float> phases;

        public float baseFrequency;
        public int sampleRate;
        public float baseVolume;
        public float duration;
        public int harmonicsCount;
        public int sampleCount;
        public int waveformType; // enum as int
        public StableHashService.EnvelopeParams envelope;

        public void Execute(int index)
        {
            float t = (float)index / sampleRate;
            float s = 0f;

            for (int h = 0; h < harmonicsCount; h++)
            {
                float freq = baseFrequency * harmonics[h];
                float phase = phases[h];
                float arg = 2f * math.PI * freq * t + phase;

                float osc = 0f;
                switch (waveformType)
                {
                    case 0: // Sine
                        osc = math.sin(arg);
                        break;
                    case 1: // Saw
                        osc = 2f * (arg / (2f * math.PI) - math.floor(arg / (2f * math.PI) + 0.5f));
                        break;
                    case 2: // Square
                        osc = math.sign(math.sin(arg));
                        break;
                    case 3: // Triangle
                        osc = 2f * math.abs(2f * (arg / (2f * math.PI) - math.floor(arg / (2f * math.PI) + 0.5f))) - 1f;
                        break;
                }

                s += osc * (1f / (1f + h * 0.6f));
            }

            float env = CalculateEnvelope(t, duration, envelope);
            float sample = math.tanh(s * 0.8f) * env * baseVolume;
            samples[index] = sample;
        }

        private static float CalculateEnvelope(float t, float totalDuration, StableHashService.EnvelopeParams env)
        {
            if (totalDuration <= 0f) return 0f;

            float attackEnd = env.Attack;
            float decayEnd = attackEnd + env.Decay;
            float sustainEnd = totalDuration - env.Release;

            if (t < attackEnd)
                return t / math.max(attackEnd, 1e-6f);

            if (t < decayEnd)
            {
                float p = (t - attackEnd) / math.max(env.Decay, 1e-6f);
                return math.lerp(1f, env.Sustain, p);
            }

            if (t < sustainEnd)
                return env.Sustain;

            float rp = (t - sustainEnd) / math.max(env.Release, 1e-6f);
            return math.lerp(env.Sustain, 0f, rp);
        }
    }
    public static async UniTask GenerateSamplesToManagedAsync(
        StableHashService.HashData hash,
        int sampleRate,
        float duration,
        float baseFrequency,
        float baseVolume,
        WaveformType waveform,
        float[] target,
        CancellationToken ct = default)
    {
        int sampleCount = Mathf.CeilToInt(sampleRate * math.max(0.001f, duration));
        if (target == null || target.Length < sampleCount)
            throw new System.ArgumentException("target buffer is null or too small", nameof(target));

        var samplesNative = new NativeArray<float>(sampleCount, Allocator.TempJob);
        var harmonicsNative = new NativeArray<float>(hash.Harmonics.Length, Allocator.TempJob);
        var phasesNative = new NativeArray<float>(hash.Phases.Length, Allocator.TempJob);

        harmonicsNative.CopyFrom(hash.Harmonics);
        phasesNative.CopyFrom(hash.Phases);

        var job = new SynthJob
        {
            samples = samplesNative,
            harmonics = harmonicsNative,
            phases = phasesNative,
            baseFrequency = baseFrequency,
            sampleRate = sampleRate,
            baseVolume = baseVolume,
            duration = duration,
            harmonicsCount = hash.Harmonics.Length,
            sampleCount = sampleCount,
            waveformType = (int)waveform,
            envelope = hash.Envelope
        };

        var handle = job.Schedule(sampleCount, math.min(1024, sampleCount));
        JobHandle.ScheduleBatchedJobs();

        await UniTask.WaitUntil(() => handle.IsCompleted, cancellationToken: ct);
        handle.Complete();
        samplesNative.CopyTo(target);

        samplesNative.Dispose();
        harmonicsNative.Dispose();
        phasesNative.Dispose();
    }
}