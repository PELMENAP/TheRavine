using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public enum WaveformType { Sine, Saw, Square, Triangle }

public static class StableHashService
{
    public readonly struct EnvelopeParams
    {
        public readonly float Attack, Decay, Sustain, Release;

        public EnvelopeParams(float a, float d, float s, float r, float duration)
        {
            if (Sustain_negative_guard(s)) s = 0f;

            float sum = a + d + r;
            if (sum > duration && sum > 1e-6f)
            {
                float scale = duration / sum;
                a *= scale;
                d *= scale;
                r *= scale;
            }

            Attack = a;
            Decay = d;
            Sustain = math.saturate(s);
            Release = r;
        }

        private static bool Sustain_negative_guard(float s) => s < 0f;
    }

    public readonly struct HashData
    {
        public readonly float[] HarmonicFreqMuls;
        public readonly float[] HarmonicAmplitudes;
        public readonly float[] HarmonicPhases;
        public readonly float[] HarmonicDecayRates;
        public readonly float[] HarmonicDriftPhases;
        public readonly EnvelopeParams Envelope;
        public readonly float InharmonicityB;
        public readonly float PitchStartMul;
        public readonly float PitchDropTime;
        public readonly float VibratoFrequency;
        public readonly float VibratoDepthCents;
        public readonly float VibratoDelay;
        public readonly float DriftAmplitude;
        public readonly float AmplitudeModDepth;
        public readonly float FmRatio;
        public readonly float FmDepth;
        public readonly float RingModFrequency;
        public readonly float WaveMorphAmount;

        public HashData(
            float[] freqMuls, float[] amplitudes, float[] phases,
            float[] decayRates, float[] driftPhases,
            EnvelopeParams envelope,
            float inharmonicityB, float pitchStartMul, float pitchDropTime,
            float vibratoFreq, float vibratoDepthCents, float vibratoDelay,
            float driftAmplitude, float amplitudeModDepth,
            float fmRatio, float fmDepth, float ringModFreq, float waveMorphAmount)
        {
            HarmonicFreqMuls = freqMuls;
            HarmonicAmplitudes = amplitudes;
            HarmonicPhases = phases;
            HarmonicDecayRates = decayRates;
            HarmonicDriftPhases = driftPhases;
            Envelope = envelope;
            InharmonicityB = inharmonicityB;
            PitchStartMul = pitchStartMul;
            PitchDropTime = pitchDropTime;
            VibratoFrequency = vibratoFreq;
            VibratoDepthCents = vibratoDepthCents;
            VibratoDelay = vibratoDelay;
            DriftAmplitude = driftAmplitude;
            AmplitudeModDepth = amplitudeModDepth;
            FmRatio = fmRatio;
            FmDepth = fmDepth;
            RingModFrequency = ringModFreq;
            WaveMorphAmount = waveMorphAmount;
        }
    }

    private static readonly Dictionary<string, HashData> cache = new(64);
    private static readonly LinkedList<string> lruList = new();
    private static readonly Dictionary<string, LinkedListNode<string>> lruNodes = new(64);
    private static int capacity = 64;
    private static readonly object gate = new();

    public static void Configure(int newCapacity) => capacity = math.max(1, newCapacity);

    public static HashData GetOrCreate(string input, int harmonicsCount, float duration = 1f)
    {
        var key = string.Concat(input ?? string.Empty, "\x01", harmonicsCount.ToString());

        lock (gate)
        {
            if (cache.TryGetValue(key, out var hit))
            {
                Touch(key);
                return hit;
            }

            var data = Compute(input, harmonicsCount, duration);

            if (cache.Count >= capacity)
                EvictOldest();

            cache[key] = data;
            var node = lruList.AddLast(key);
            lruNodes[key] = node;

            return data;
        }
    }

    public static void Clear()
    {
        lock (gate)
        {
            cache.Clear();
            lruList.Clear();
            lruNodes.Clear();
        }
    }

    private static void Touch(string key)
    {
        var node = lruNodes[key];
        lruList.Remove(node);
        var newNode = lruList.AddLast(key);
        lruNodes[key] = newNode;
    }

    private static void EvictOldest()
    {
        var oldest = lruList.First;
        if (oldest == null) return;

        lruList.RemoveFirst();
        lruNodes.Remove(oldest.Value);
        cache.Remove(oldest.Value);
    }

    private static HashData Compute(string input, int harmonicsCount, float duration)
    {
        var normalized = (input ?? string.Empty).Normalize(NormalizationForm.FormKD);

        var runes = new List<int>(8);
        var enumerator = StringInfo.GetTextElementEnumerator(normalized);
        while (enumerator.MoveNext() && runes.Count < 8)
            runes.Add(char.ConvertToUtf32(enumerator.GetTextElement(), 0));
        while (runes.Count < 8) runes.Add(0);

        ulong h1 = 5381, h2 = 2166136261UL;
        foreach (var r in runes)
        {
            h1 = ((h1 << 5) + h1) ^ (uint)r;
            h2 = (h2 ^ (uint)r) * 16777619UL;
        }

        uint seed = (uint)(h1 ^ h2);
        var rng = new Random(seed == 0 ? 1u : seed);

        var freqMuls = new float[harmonicsCount];
        var amplitudes = new float[harmonicsCount];
        var phases = new float[harmonicsCount];
        var decayRates = new float[harmonicsCount];
        var driftPhases = new float[harmonicsCount];

        float spectralTilt = math.lerp(0.3f, 0.95f, (h2 >> 16 & 0xFF) / 255f);
        float amplitudeSum = 0f;
        float harmonicCountInv = 1f / math.max(harmonicsCount - 1, 1);

        float inharmonicityB = math.lerp(0f, 0.0008f, rng.NextFloat());

        for (int i = 0; i < harmonicsCount; i++)
        {
            int n = i + 1;
            float physical = n * math.sqrt(1f + inharmonicityB * n * n);
            float smallJitter = math.lerp(-0.01f, 0.01f, i < runes.Count ? (runes[i] & 0xFF) / 255f : 0.5f);
            freqMuls[i] = physical + smallJitter;

            amplitudes[i] = math.pow(spectralTilt, i) + rng.NextFloat() * 0.3f;
            amplitudeSum += amplitudes[i];

            phases[i] = i > 0 ? rng.NextFloat(0f, 2f * math.PI) * 0.5f : 0f;
            decayRates[i] = math.lerp(0.4f, 4f, i * harmonicCountInv);
            driftPhases[i] = rng.NextFloat(0f, 2f * math.PI);
        }

        for (int i = 0; i < harmonicsCount; i++)
            amplitudes[i] /= amplitudeSum;

        var env = new EnvelopeParams(
            math.lerp(0.005f, 0.15f, (h1 & 0xFF) / 255f),
            math.lerp(0.02f, 0.3f, (h1 >> 8 & 0xFF) / 255f),
            math.lerp(0.2f, 0.9f, (h2 >> 8 & 0xFF) / 255f),
            math.lerp(0.05f, 0.6f, (h2 >> 16 & 0xFF) / 255f),
            duration
        );

        return new HashData(
            freqMuls, amplitudes, phases, decayRates, driftPhases, env,
            inharmonicityB: inharmonicityB,
            pitchStartMul:  math.lerp(1f, 4f, (h2 & 0xFF) / 255f),
            pitchDropTime:  math.lerp(0.01f, 0.2f, (h1 >> 16 & 0xFF) / 255f),
            vibratoFreq:        math.lerp(4f, 8f, rng.NextFloat()),
            vibratoDepthCents:  math.lerp(0f, 60f, rng.NextFloat()),
            vibratoDelay:       math.lerp(0.05f, 0.3f, rng.NextFloat()),
            driftAmplitude:     math.lerp(0f, 2f, rng.NextFloat()),
            amplitudeModDepth:  math.lerp(0f, 0.2f, rng.NextFloat()),
            fmRatio:            math.lerp(0.5f, 4f, rng.NextFloat()),
            fmDepth:            math.lerp(0f, 3f, rng.NextFloat()),
            ringModFreq:        math.lerp(0f, 180f, rng.NextFloat()),
            waveMorphAmount:    rng.NextFloat()
        );
    }
}