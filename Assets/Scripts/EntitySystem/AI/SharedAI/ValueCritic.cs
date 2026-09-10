using System;

public sealed class ValueCritic
{
    private const int Hidden = 64;

    private readonly float[] _w1;
    private readonly float[] _b1;
    private readonly float[] _w2;
    private readonly float[] _h;
    private float _b2;

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
        _h  = new float[Hidden];

        float s1 = MathF.Sqrt(2f / (inputSize + Hidden));
        for (int i = 0; i < _w1.Length; i++) _w1[i] = RavineRandom.RangeFloat(-s1, s1);

        float s2 = MathF.Sqrt(2f / (Hidden + 1));
        for (int i = 0; i < Hidden; i++) _w2[i] = RavineRandom.RangeFloat(-s2, s2);
    }

    public float Predict(float[] x)
    {
        float sum = _b2;
        for (int n = 0; n < Hidden; n++)
        {
            int off = n * _inputSize;
            float pre = _b1[n];
            for (int i = 0; i < _inputSize; i++) pre += _w1[off + i] * x[i];

            float a = MathF.Tanh(pre);
            _h[n] = a;
            sum += _w2[n] * a;
        }
        return sum;
    }

    public float TrainTD(float[] x, float target)
    {
        float error = target - Predict(x);
        if (!float.IsFinite(error)) return 0f;

        float delta = error > _huberDelta ? _huberDelta
                    : (error < -_huberDelta ? -_huberDelta : error);

        float step = _lr * delta;
        _b2 += step;

        for (int n = 0; n < Hidden; n++)
        {
            float a  = _h[n];
            float dh = step * _w2[n] * (1f - a * a);

            _w2[n] += step * a;
            _b1[n] += dh;

            int off = n * _inputSize;
            for (int i = 0; i < _inputSize; i++) _w1[off + i] += dh * x[i];
        }

        return error;
    }
}