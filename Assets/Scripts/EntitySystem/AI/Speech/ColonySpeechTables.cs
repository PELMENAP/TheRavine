using Unity.Mathematics;

public sealed class ColonySpeechTables
{
    public const int Contexts = 1 << SimulationRules.SpeechContextBits;
    public const int Vocab    = SimulationRules.SpeechVocab;
    public const int Actions  = ActionCatalog.Count;

    private readonly int[] _emit   = new int[Contexts * Vocab];
    private readonly int[] _listen = new int[2 * Actions];

    public void Clear()
    {
        System.Array.Clear(_emit, 0, _emit.Length);
        System.Array.Clear(_listen, 0, _listen.Length);
    }

    public void Add(ColonySpeechTables other)
    {
        for (int i = 0; i < _emit.Length; i++) _emit[i] += other._emit[i];
        for (int i = 0; i < _listen.Length; i++) _listen[i] += other._listen[i];
    }

    public void RecordEmission(in Utterance u)
    {
        int row = (u.Context & (Contexts - 1)) * Vocab;
        int len = math.min((int)u.Len, SimulationRules.SpeechLen);
        for (int s = 0; s < len; s++) _emit[row + u.Token(s)]++;
    }

    public void RecordListen(bool fresh, int action)
    {
        if ((uint)action >= (uint)Actions) return;
        _listen[(fresh ? Actions : 0) + action]++;
    }

    public float MutualInformationBits()
    {
        long total = 0;
        for (int i = 0; i < _emit.Length; i++) total += _emit[i];
        if (total == 0) return 0f;

        double inv = 1.0 / total;
        double mi = 0.0;
        int usedCtx = 0, usedTok = 0;
        for (int c = 0; c < Contexts; c++)
        {
            long rowSum = 0;
            for (int t = 0; t < Vocab; t++) rowSum += _emit[c * Vocab + t];
            if (rowSum == 0) continue;
            usedCtx++;
            double pc = rowSum * inv;
            for (int t = 0; t < Vocab; t++)
            {
                int n = _emit[c * Vocab + t];
                if (n == 0) continue;
                long colSum = 0;
                for (int k = 0; k < Contexts; k++) colSum += _emit[k * Vocab + t];
                double pt = colSum * inv;
                double pj = n * inv;
                mi += pj * System.Math.Log(pj / (pc * pt));
            }
        }
        for (int t = 0; t < Vocab; t++)
        {
            for (int c = 0; c < Contexts; c++)
            {
                if (_emit[c * Vocab + t] == 0) continue;
                usedTok++;
                break;
            }
        }

        const double Ln2 = 0.6931471805599453;
        double bias = (usedCtx - 1) * (usedTok - 1) / (2.0 * total);
        return (float)math.max((mi - bias) / Ln2, 0.0);
    }

    public float ListenerShift()
    {
        long none = 0, fresh = 0;
        for (int a = 0; a < Actions; a++)
        {
            none  += _listen[a];
            fresh += _listen[Actions + a];
        }
        if (none == 0 || fresh == 0) return 0f;

        double invNone = 1.0 / none, invFresh = 1.0 / fresh;
        double sum = 0.0;
        for (int a = 0; a < Actions; a++)
            sum += System.Math.Abs(_listen[Actions + a] * invFresh - _listen[a] * invNone);
        return (float)(0.5 * sum);
    }
}
