using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public enum WaveformType { Sine, Saw, Square, Triangle }
public static class StableHashService
{
    public readonly struct HashData
    {
        public readonly float[] Harmonics;
        public readonly float[] Phases;
        public readonly EnvelopeParams Envelope;

        public HashData(float[] harmonics, float[] phases, EnvelopeParams envelope)
        {
            Harmonics = harmonics;
            Phases = phases;
            Envelope = envelope;
        }
    }

    [Serializable]
    public readonly struct EnvelopeParams
    {
        public readonly float Attack;
        public readonly float Decay;
        public readonly float Sustain;
        public readonly float Release;

        public EnvelopeParams(float a, float d, float s, float r)
        {
            Attack = a; Decay = d; Sustain = s; Release = r;
        }
    }
    public static HashData CreateHashData(string input, int harmonicsCount)
    {
        var normalized = input ?? string.Empty;
        normalized = normalized.Normalize(NormalizationForm.FormKD);

        var runes = new List<int>(8);
        var enumerator = StringInfo.GetTextElementEnumerator(normalized);
        while (enumerator.MoveNext() && runes.Count < 8)
        {
            var elem = enumerator.GetTextElement();
            int codepoint = char.ConvertToUtf32(elem, 0);
            runes.Add(codepoint);
        }

        while (runes.Count < 8) runes.Add(0);

        ulong h1 = 5381;
        ulong h2 = 2166136261UL;
        foreach (var r in runes)
        {
            h1 = ((h1 << 5) + h1) ^ (uint)r;
            h2 = (h2 ^ (uint)r) * 16777619UL;
        }

        uint seed = (uint)(h1 ^ h2);
        var rng = new Random(seed == 0 ? 1u : seed);

        var harmonics = new float[harmonicsCount];
        var phases = new float[harmonicsCount];

        for (int i = 0; i < harmonicsCount; i++)
        {
            float byteInfluence = (i < runes.Count) ? ((runes[i] & 0xFF) / 255f) : 0f;
            float harmonicDecay = 1f / (1f + i * 0.5f);

            harmonics[i] = math.lerp(0.6f + i * 0.2f, 1f + i * 1.2f, byteInfluence) * harmonicDecay;
            phases[i] = rng.NextFloat(0f, math.PI * 2f);
        }

        var env = new EnvelopeParams(
            math.lerp(0.005f, 0.15f, (h1 & 0xFF) / 255f),
            math.lerp(0.02f, 0.3f, ((h1 >> 8) & 0xFF) / 255f),
            math.lerp(0.2f, 0.9f, ((h2 >> 8) & 0xFF) / 255f),
            math.lerp(0.05f, 0.6f, ((h2 >> 16) & 0xFF) / 255f)
        );

        return new HashData(harmonics, phases, env);
    }
}