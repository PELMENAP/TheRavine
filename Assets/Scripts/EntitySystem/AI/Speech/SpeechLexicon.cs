using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "SpeechLexicon", menuName = "Simulation/Speech Lexicon")]
public sealed class SpeechLexicon : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string word;
        [Min(0)] public int token;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();
    [SerializeField] private string[] tokenLabels = Array.Empty<string>();

    private static SpeechLexicon _default;
    private static readonly string[] DefaultLabels = BuildDefaultLabels();
    private static readonly string[] MeaningCache = new string[1 << SimulationRules.SpeechContextBits];

    private readonly Dictionary<int, string> _utteranceLabels = new();

    public static SpeechLexicon Default
    {
        get
        {
            if (_default == null)
            {
                _default = Resources.Load<SpeechLexicon>("SpeechLexicon");
                if (_default == null) _default = CreateInstance<SpeechLexicon>();
            }
            return _default;
        }
    }

    private static string[] BuildDefaultLabels()
    {
        var labels = new string[SimulationRules.SpeechVocab];
        for (int i = 0; i < labels.Length; i++) labels[i] = "t" + i;
        return labels;
    }

    public string TokenLabel(int token)
    {
        if ((uint)token >= (uint)SimulationRules.SpeechVocab) return string.Empty;
        if (tokenLabels != null && token < tokenLabels.Length && !string.IsNullOrEmpty(tokenLabels[token])) return tokenLabels[token];
        return DefaultLabels[token];
    }

    public int Encode(string text, out ushort tokens)
    {
        tokens = 0;
        if (string.IsNullOrEmpty(text)) return 0;

        var span = text.AsSpan();
        int len = 0;
        int start = -1;
        for (int i = 0; i <= span.Length && len < SimulationRules.SpeechLen; i++)
        {
            bool separator = i == span.Length || char.IsWhiteSpace(span[i]) || char.IsPunctuation(span[i]);
            if (!separator)
            {
                if (start < 0) start = i;
                continue;
            }
            if (start < 0) continue;

            if (TryResolve(span.Slice(start, i - start), out int token))
                tokens = Utterance.SetToken(tokens, len++, token);
            start = -1;
        }
        return len;
    }

    private bool TryResolve(ReadOnlySpan<char> word, out int token)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.IsNullOrEmpty(entries[i].word)) continue;
                if (!word.Equals(entries[i].word.AsSpan(), StringComparison.OrdinalIgnoreCase)) continue;
                token = entries[i].token;
                return (uint)token < (uint)SimulationRules.SpeechVocab;
            }
        }

        token = -1;
        if (word.Length < 2 || (word[0] != 't' && word[0] != 'T')) return false;

        int value = 0;
        for (int i = 1; i < word.Length; i++)
        {
            if (word[i] < '0' || word[i] > '9') return false;
            value = value * 10 + (word[i] - '0');
            if (value >= SimulationRules.SpeechVocab) return false;
        }
        token = value;
        return true;
    }

    public string Label(ushort tokens, int len)
    {
        int key = tokens | (len << 16);
        if (_utteranceLabels.TryGetValue(key, out var cached)) return cached;

        var sb = new StringBuilder();
        for (int i = 0; i < len; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(TokenLabel((tokens >> (i * SimulationRules.SpeechTokenBits)) & (SimulationRules.SpeechVocab - 1)));
        }
        var label = sb.ToString();
        _utteranceLabels[key] = label;
        return label;
    }

    public static string Meaning(int token, float minShare = 0.5f, long minSamples = 8)
    {
        int mask = SpeechMetrics.ContextMask(token, minShare, out long samples);
        if (samples < minSamples || mask == 0) return string.Empty;

        var cached = MeaningCache[mask];
        if (cached != null) return cached;

        var sb = new StringBuilder();
        for (int b = 0; b < SimulationRules.SpeechContextBits; b++)
        {
            if ((mask & (1 << b)) == 0) continue;
            if (sb.Length > 0) sb.Append('+');
            sb.Append(SpeechMetrics.ContextNames[b]);
        }
        cached = sb.ToString();
        MeaningCache[mask] = cached;
        return cached;
    }
}
