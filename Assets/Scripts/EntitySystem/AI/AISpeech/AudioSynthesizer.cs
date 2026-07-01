using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class AudioSynthesizer
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    private struct SynthJob : IJobParallelFor
    {
        public NativeArray<float> Samples;
        [ReadOnly] public NativeArray<HarmonicPreset> Harmonics;

        public float BaseFrequency;
        public float SampleRate;
        public float InvSampleRate;
        public float BaseVolume;
        public float Duration;
        public int HarmonicsCount;
        public int PrimaryWaveform;
        public int SecondaryWaveform;
        public float WaveMorphAmount;
        public float PitchStartMul;
        public float PitchDropTime;
        public float VibratoFrequency;
        public float VibratoDepthCents;
        public float VibratoDelay;
        public float VibratoRampTime;
        public float DriftAmplitude;
        public float DriftFrequency;
        public float AmplitudeModDepth;
        public float AmplitudeModFrequency;
        public float FmRatio;
        public float FmDepth;
        public float RingModFrequency;
        public float InharmonicityB;
        public float NoiseAmount;
        public float SaturationAmount;
        public float Stress;
        public float StressDecayMultiplier;
        public float Brightness;
        public float AmplitudeNormDenom;
        public float HarmonicCountInv;

        public StableHashService.EnvelopeParams Envelope;

        public void Execute(int index)
        {
            const float TwoPi = 2f * math.PI;
            float nyquistLimit = SampleRate * 0.45f;
            float t = index * InvSampleRate;

            float pitchT = math.saturate(t / math.max(PitchDropTime, 0.001f));
            float currentPitchMul = math.lerp(PitchStartMul, 1f, pitchT);

            float vibratoRamp = math.saturate((t - VibratoDelay) / math.max(VibratoRampTime, 0.001f));
            float vibratoLfo = math.sin(TwoPi * VibratoFrequency * t) * vibratoRamp;
            float vibratoMul = math.pow(2f, (VibratoDepthCents * vibratoLfo) / 1200f);

            float currentBaseFreq = BaseFrequency * currentPitchMul * vibratoMul;
            float fmOffset = FmDepth * math.sin(TwoPi * currentBaseFreq * FmRatio * t);

            float s = 0f;

            for (int h = 0; h < HarmonicsCount; h++)
            {
                var preset = Harmonics[h];
                float harmonicIndex = h * HarmonicCountInv;

                float drift = DriftAmplitude * math.sin(TwoPi * DriftFrequency * t + preset.DriftPhase);
                
                float inharmonicFactor = math.sqrt(1f + InharmonicityB * preset.FreqMul * preset.FreqMul);
                float freq = currentBaseFreq * preset.FreqMul * inharmonicFactor + drift;

                float nyquistFade = math.saturate((nyquistLimit - freq) / (nyquistLimit * 0.1f));
                if (nyquistFade <= 0f) continue;

                float arg = TwoPi * freq * t + preset.Phase + fmOffset;
                float osc = MorphOscillator(PrimaryWaveform, SecondaryWaveform, arg, WaveMorphAmount);

                float amp = preset.Amplitude * math.lerp(1f, 1f + Brightness * 0.5f, harmonicIndex) / AmplitudeNormDenom;
                float decayRate = preset.DecayRate * math.lerp(1f, 1f + Stress * StressDecayMultiplier, harmonicIndex);
                float harmonicDecay = math.exp(-t * decayRate);

                float ampMod = 1f + AmplitudeModDepth * math.sin(TwoPi * AmplitudeModFrequency * t + preset.DriftPhase * 1.7f);

                s += osc * amp * harmonicDecay * ampMod * nyquistFade;
            }

            float ringMod = RingModFrequency > 0.5f ? math.sin(TwoPi * RingModFrequency * t) : 1f;
            float env = EvaluateEnvelope(t, Duration, Envelope);
            
            float noiseFac = NoiseAmount * (noise.cnoise(new float2(t * 1000f, index * 0.1f)) * 2f - 1f);
            
            float mixedSignal = s + noiseFac;
            
            float saturated = math.tanh(mixedSignal * SaturationAmount) / math.max(1f, SaturationAmount * 0.7f);

            Samples[index] = saturated * ringMod * env * BaseVolume;
        }

        private static float MorphOscillator(int primary, int secondary, float arg, float morphT)
        {
            float a = Oscillator(primary, arg);
            return morphT < 0.001f ? a : math.lerp(a, Oscillator(secondary, arg), morphT);
        }

        private static float Oscillator(int type, float arg)
        {
            const float TwoPi = 2f * math.PI;
            switch (type)
            {
                case 0: return math.sin(arg);
                case 1: float tSaw = arg / TwoPi; return 2f * (tSaw - math.floor(tSaw + 0.5f));
                case 2: return math.sin(arg) >= 0f ? 1f : -1f;
                case 3: float tTri = arg / TwoPi; return 2f * math.abs(2f * (tTri - math.floor(tTri + 0.5f))) - 1f;
                default: return 0f;
            }
        }

        private static float EvaluateEnvelope(float t, float duration, StableHashService.EnvelopeParams env)
        {
            if (duration <= 0f) return 0f;
            float attackEnd = env.Attack;
            float decayEnd = attackEnd + env.Decay;
            float sustainEnd = duration - env.Release;

            if (t < attackEnd) return t / math.max(attackEnd, 1e-6f);
            if (t < decayEnd) return math.lerp(1f, env.Sustain, (t - attackEnd) / math.max(env.Decay, 1e-6f));
            if (t < sustainEnd) return env.Sustain;
            return math.lerp(env.Sustain, 0f, (t - sustainEnd) / math.max(env.Release, 1e-6f));
        }
    }

    public static async UniTask GenerateSamplesToManagedAsync(
        AgentGeneticAudioProfile genetic,
        AgentAudioState state,
        NativeArray<float> samplesBuffer,
        int sampleRate,
        float duration,
        float[] target,
        CancellationToken ct = default)
    {
        int sampleCount = Mathf.CeilToInt(sampleRate * math.max(0.001f, duration));

        if (target == null || target.Length < sampleCount)
            throw new System.ArgumentException("target слишком мал или null", nameof(target));
        if (samplesBuffer.Length < sampleCount)
            throw new System.ArgumentException("samplesBuffer слишком мал", nameof(samplesBuffer));

        var job = new SynthJob
        {
            Samples = samplesBuffer,
            Harmonics = genetic.Harmonics,
            BaseFrequency = state.BaseFrequency,
            SampleRate = sampleRate,
            InvSampleRate = 1f / sampleRate,
            BaseVolume = state.BaseVolume,
            Duration = duration,
            HarmonicsCount = genetic.HarmonicsCount,
            HarmonicCountInv = 1f / math.max(genetic.HarmonicsCount - 1, 1),
            PrimaryWaveform = (int)state.Primary,
            SecondaryWaveform = (int)state.Secondary,
            WaveMorphAmount = state.WaveMorphAmount,
            PitchStartMul = state.PitchStartMul,
            PitchDropTime = state.PitchDropTime,
            VibratoFrequency = state.VibratoFrequency,
            VibratoDepthCents = state.VibratoDepthCents,
            VibratoDelay = state.VibratoDelay,
            VibratoRampTime = state.VibratoRampTime,
            DriftAmplitude = state.DriftAmplitude,
            DriftFrequency = state.DriftFrequency,
            AmplitudeModDepth = state.AmplitudeModDepth,
            AmplitudeModFrequency = state.AmplitudeModFrequency,
            FmRatio = state.FmRatio,
            FmDepth = state.FmDepth,
            RingModFrequency = state.RingModFrequency,
            InharmonicityB = state.InharmonicityB,
            NoiseAmount = state.NoiseAmount,
            SaturationAmount = state.SaturationAmount,
            Brightness = state.Brightness,
            Stress = state.Stress,
            StressDecayMultiplier = state.StressDecayMultiplier,
            AmplitudeNormDenom = state.AmplitudeNormDenom,
            Envelope = genetic.BaseHash.Envelope
        };

        var handle = job.Schedule(sampleCount, math.min(1024, sampleCount));

        try
        {
            await UniTask.WaitUntil(() => handle.IsCompleted, PlayerLoopTiming.Update, ct);
            handle.Complete();

            if (!ct.IsCancellationRequested)
                NativeArray<float>.Copy(samplesBuffer, target, sampleCount);
        }
        finally
        {
            if (!handle.IsCompleted) handle.Complete();
        }
    }
}