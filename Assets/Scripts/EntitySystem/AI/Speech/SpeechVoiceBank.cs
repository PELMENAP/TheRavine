using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct SpeechSyllableJob : IJobParallelFor
{
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> Samples;
    public int   SamplesPerClip;
    public int   Vocab;
    public int   SampleRate;
    public float BaseFrequency;
    public float VoiceStep;
    public float Gain;

    public void Execute(int entry)
    {
        int voice = entry / Vocab;
        int token = entry - voice * Vocab;
        int level = token & 3;
        int shape = token >> 2;

        float semitones = level * 4f - VoiceStep * voice;
        float f0 = BaseFrequency * math.exp2(semitones * (1f / 12f));

        float4 w = new float4(0.3f, 0.4f, 1f, 0.6f);
        w = math.select(w, new float4(0.6f, 1f, 0.2f, 0f), shape == 2);
        w = math.select(w, new float4(1f, 0.7f, 0.3f, 0.1f), shape == 1);
        w = math.select(w, new float4(1f, 0f, 0f, 0f), shape == 0);
        float norm = Gain / (w.x + w.y + w.z + w.w);

        float omega = 2f * math.PI * f0 / SampleRate;
        float invN  = 1f / math.max(SamplesPerClip - 1, 1);
        int   baseIndex = entry * SamplesPerClip;

        for (int i = 0; i < SamplesPerClip; i++)
        {
            float phase = omega * i;
            float s = w.x * math.sin(phase)
                    + w.y * math.sin(2f * phase)
                    + w.z * math.sin(3f * phase)
                    + w.w * math.sin(4f * phase);
            float env = math.sin(math.PI * i * invN);
            Samples[baseIndex + i] = s * env * norm;
        }
    }
}

public sealed class SpeechVoiceBank : System.IDisposable
{
    public const int VoiceClasses = Utterance.VoiceMask + 1;

    private readonly AudioClip[] _clips;

    public SpeechVoiceBank(int sampleRate, float syllableSeconds, float baseFrequency, float voiceStep, float gain)
    {
        int vocab   = SimulationRules.SpeechVocab;
        int entries = vocab * VoiceClasses;
        int samples = Mathf.Max(16, Mathf.CeilToInt(sampleRate * syllableSeconds));

        var buffer = new NativeArray<float>(entries * samples, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        try
        {
            new SpeechSyllableJob
            {
                Samples        = buffer,
                SamplesPerClip = samples,
                Vocab          = vocab,
                SampleRate     = sampleRate,
                BaseFrequency  = baseFrequency,
                VoiceStep      = voiceStep,
                Gain           = gain,
            }.Schedule(entries, 8).Complete();

            _clips = new AudioClip[entries];
            var managed = new float[samples];
            for (int e = 0; e < entries; e++)
            {
                buffer.Slice(e * samples, samples).CopyTo(managed);
                var clip = AudioClip.Create("SpeechSyllable", samples, 1, sampleRate, false);
                clip.SetData(managed, 0);
                _clips[e] = clip;
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    public AudioClip Get(int voice, int token)
        => _clips[(voice & Utterance.VoiceMask) * SimulationRules.SpeechVocab + (token & (SimulationRules.SpeechVocab - 1))];

    public static int ClassOf(in GeneticParameters genes)
    {
        var range = GeneticParameters.ParameterRanges[GeneticParameters.IdxMaxEnergyMul];
        float t = math.saturate((genes.MaxEnergyMul - range.min) / math.max(range.max - range.min, 1e-6f));
        return math.min((int)(t * VoiceClasses), VoiceClasses - 1);
    }

    public void Dispose()
    {
        if (_clips == null) return;
        for (int i = 0; i < _clips.Length; i++)
        {
            if (_clips[i] == null) continue;
            if (Application.isPlaying) Object.Destroy(_clips[i]);
            else Object.DestroyImmediate(_clips[i]);
            _clips[i] = null;
        }
    }
}
