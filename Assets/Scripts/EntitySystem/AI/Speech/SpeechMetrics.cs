using Unity.Mathematics;

public static class SpeechMetrics
{
    private const int Vocab = SimulationRules.SpeechVocab;
    private const int Bits  = SimulationRules.SpeechContextBits;

    public static readonly string[] ContextNames = { "danger", "food", "carry", "sick", "nest", "breed" };

    private static readonly long[] TokenCounts = new long[Vocab];
    private static readonly long[] Cooc        = new long[Vocab * Bits];
    private static readonly long[] BitOnes     = new long[Bits];
    private static readonly long[] DecodeOnes  = new long[Bits];
    private static long _tokenTotal;
    private static long _decodeEvents;
    private static long _decodeBitsCorrect;

    public static long TokenTotal   => _tokenTotal;
    public static long DecodeEvents => _decodeEvents;

    public static void Reset()
    {
        System.Array.Clear(TokenCounts, 0, TokenCounts.Length);
        System.Array.Clear(Cooc, 0, Cooc.Length);
        System.Array.Clear(BitOnes, 0, BitOnes.Length);
        System.Array.Clear(DecodeOnes, 0, DecodeOnes.Length);
        _tokenTotal = 0;
        _decodeEvents = 0;
        _decodeBitsCorrect = 0;
    }

    public static void RecordEmission(in Utterance u)
    {
        if (u.IsReflex || u.Speaker < 0) return;

        int len = math.min((int)u.Len, SimulationRules.SpeechLen);
        for (int s = 0; s < len; s++)
        {
            int token = u.Token(s);
            TokenCounts[token]++;
            _tokenTotal++;
            for (int b = 0; b < Bits; b++)
            {
                if (((u.Context >> b) & 1) == 0) continue;
                Cooc[token * Bits + b]++;
                BitOnes[b]++;
            }
        }
    }

    public static void RecordDecode(int correctBits, int target)
    {
        _decodeEvents++;
        _decodeBitsCorrect += correctBits;
        for (int b = 0; b < Bits; b++) DecodeOnes[b] += (target >> b) & 1;
    }

    public static float TokenEntropy()
    {
        if (_tokenTotal == 0) return 0f;
        float inv = 1f / _tokenTotal;
        float h = 0f;
        for (int t = 0; t < Vocab; t++)
        {
            float p = TokenCounts[t] * inv;
            if (p > 0f) h -= p * math.log2(p);
        }
        return h;
    }

    public static float MutualInformation(int bit)
    {
        if (_tokenTotal == 0 || (uint)bit >= (uint)Bits) return 0f;
        float inv = 1f / _tokenTotal;
        float p1  = BitOnes[bit] * inv;
        float p0  = 1f - p1;
        float mi  = 0f;
        for (int t = 0; t < Vocab; t++)
        {
            float pt = TokenCounts[t] * inv;
            if (pt <= 0f) continue;
            float j1 = Cooc[t * Bits + bit] * inv;
            float j0 = pt - j1;
            if (j1 > 0f && p1 > 0f) mi += j1 * math.log2(j1 / (pt * p1));
            if (j0 > 0f && p0 > 0f) mi += j0 * math.log2(j0 / (pt * p0));
        }
        return mi;
    }

    public static float MeanMutualInformation()
    {
        float sum = 0f;
        for (int b = 0; b < Bits; b++) sum += MutualInformation(b);
        return sum / Bits;
    }

    public static float DecodeAccuracy()
        => _decodeEvents == 0 ? 0f : (float)_decodeBitsCorrect / (_decodeEvents * Bits);

    public static float DecodeBaseline()
    {
        if (_decodeEvents == 0) return 0f;
        float sum = 0f;
        for (int b = 0; b < Bits; b++)
        {
            float p = (float)DecodeOnes[b] / _decodeEvents;
            sum += math.max(p, 1f - p);
        }
        return sum / Bits;
    }

    public static long TokenCount(int token) => (uint)token < (uint)Vocab ? TokenCounts[token] : 0L;

    public static int ContextMask(int token, float minShare, out long samples)
    {
        samples = (uint)token < (uint)Vocab ? TokenCounts[token] : 0L;
        if (samples == 0) return 0;

        int mask = 0;
        for (int b = 0; b < Bits; b++)
            if (Cooc[token * Bits + b] >= minShare * samples) mask |= 1 << b;
        return mask;
    }
}
