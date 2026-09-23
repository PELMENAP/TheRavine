using System;

public sealed class ValueCritic
{
    private const int Hidden     = 64;
    private const int CacheSlots = 2;

    private readonly float[] _w1;
    private readonly float[] _b1;
    private readonly float[] _w2;
    private readonly float[] _h;
    private float _b2;

    private readonly float[][] _cacheInput = new float[CacheSlots][];
    private readonly int[]     _cacheStamp = new int[CacheSlots];
    private readonly float[]   _cacheValue = new float[CacheSlots];
    private int _lastSlot;

    private readonly int _inputSize;
    private readonly float _lr;
    private readonly float _huberDelta;

    public ValueCritic(int inputSize, float lr = -1f)
    {
        var rules = SimulationRules.Active;

        _inputSize  = inputSize;
        _lr         = lr > 0f ? lr : rules.CriticLearningRate;
        _huberDelta = rules.CriticHuberDelta;

        _w1 = new float[Hidden * inputSize];
        _b1 = new float[Hidden];
        _w2 = new float[Hidden];
        _h  = new float[Hidden * CacheSlots];

        float s1 = MathF.Sqrt(2f / (inputSize + Hidden));
        for (int i = 0; i < _w1.Length; i++) _w1[i] = RavineRandom.RangeFloat(-s1, s1);

        float s2 = MathF.Sqrt(2f / (Hidden + 1));
        for (int i = 0; i < Hidden; i++) _w2[i] = RavineRandom.RangeFloat(-s2, s2);
    }

    public float Predict(float[] x, int stamp = 0)
    {
        int slot = AcquireSlot();
        float v  = Forward(x, slot);
        _cacheInput[slot] = stamp != 0 ? x : null;
        _cacheStamp[slot] = stamp;
        _cacheValue[slot] = v;
        return v;
    }

    public float TrainTD(float[] x, float target, int stamp = 0)
    {
        int slot = FindSlot(x, stamp);
        float v;
        if (slot < 0)
        {
            slot = AcquireSlot();
            v    = Forward(x, slot);
        }
        else v = _cacheValue[slot];

        _cacheInput[slot] = null;

        float error = target - v;
        if (!float.IsFinite(error)) return 0f;

        float delta = error > _huberDelta ? _huberDelta
                    : (error < -_huberDelta ? -_huberDelta : error);

        float step = _lr * delta;
        _b2 += step;

        int hb = slot * Hidden;
        for (int n = 0; n < Hidden; n++)
        {
            float a  = _h[hb + n];
            float dh = step * _w2[n] * (1f - a * a);

            _w2[n] += step * a;
            _b1[n] += dh;

            int off = n * _inputSize;
            for (int i = 0; i < _inputSize; i++) _w1[off + i] += dh * x[i];
        }

        return error;
    }

    private float Forward(float[] x, int slot)
    {
        int hb = slot * Hidden;
        float sum = _b2;
        for (int n = 0; n < Hidden; n++)
        {
            int off = n * _inputSize;
            float pre = _b1[n];
            for (int i = 0; i < _inputSize; i++) pre += _w1[off + i] * x[i];

            float a = MathF.Tanh(pre);
            _h[hb + n] = a;
            sum += _w2[n] * a;
        }
        return sum;
    }

    private int FindSlot(float[] x, int stamp)
    {
        if (stamp == 0) return -1;
        for (int s = 0; s < CacheSlots; s++)
            if (_cacheStamp[s] == stamp && ReferenceEquals(_cacheInput[s], x)) return s;
        return -1;
    }

    private int AcquireSlot()
    {
        int slot;
        if (_cacheInput[0] == null)      slot = 0;
        else if (_cacheInput[1] == null) slot = 1;
        else                             slot = _lastSlot ^ 1;
        _lastSlot = slot;
        return slot;
    }
}