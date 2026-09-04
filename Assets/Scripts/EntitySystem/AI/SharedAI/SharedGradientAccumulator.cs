using System;

public sealed class SharedGradientAccumulator
{
    private readonly int[] _neurons;
    private readonly int[] _inputs;
    private readonly int[] _wOffset;
    private readonly int[] _bOffset;
    private readonly bool[][] _touched;

    public readonly float[] W;
    public readonly float[] Tau;
    public readonly float[] B;
    public readonly float[] TauB;

    public int Contributions { get; private set; }

    public SharedGradientAccumulator(int[] layerSizes)
    {
        int L = layerSizes.Length - 1;
        _neurons = new int[L];
        _inputs  = new int[L];
        _wOffset = new int[L];
        _bOffset = new int[L];
        _touched = new bool[L][];

        int wTotal = 0, bTotal = 0;
        for (int l = 0; l < L; l++)
        {
            _neurons[l] = layerSizes[l + 1];
            _inputs[l]  = layerSizes[l];
            _wOffset[l] = wTotal;
            _bOffset[l] = bTotal;
            wTotal += _neurons[l] * _inputs[l];
            bTotal += _neurons[l];
            _touched[l] = new bool[_neurons[l]];
        }

        W    = new float[wTotal];
        Tau  = new float[wTotal];
        B    = new float[bTotal];
        TauB = new float[bTotal];
    }

    public int Neurons(int l) => _neurons[l];
    public int Inputs(int l)  => _inputs[l];
    public int WeightIndex(int l, int n) => _wOffset[l] + n * _inputs[l];
    public int BiasIndex(int l, int n)   => _bOffset[l] + n;

    public bool IsTouched(int l, int n) => _touched[l][n];
    public void MarkTouched(int l, int n) => _touched[l][n] = true;

    public void Clear()
    {
        for (int l = 0; l < _touched.Length; l++)
        {
            bool[] t = _touched[l];
            int inputs = _inputs[l];
            for (int n = 0; n < t.Length; n++)
            {
                if (!t[n]) continue;
                t[n] = false;

                int wi = _wOffset[l] + n * inputs;
                Array.Clear(W,   wi, inputs);
                Array.Clear(Tau, wi, inputs);

                int bi = _bOffset[l] + n;
                B[bi]    = 0f;
                TauB[bi] = 0f;
            }
        }
        Contributions = 0;
    }

    public double SquaredNorm()
    {
        double sq = 0d;
        for (int l = 0; l < _touched.Length; l++)
        {
            bool[] t = _touched[l];
            int inputs = _inputs[l];
            for (int n = 0; n < t.Length; n++)
            {
                if (!t[n]) continue;

                int wi = _wOffset[l] + n * inputs;
                for (int i = 0; i < inputs; i++)
                {
                    float gw = W[wi + i], gt = Tau[wi + i];
                    sq += (double)gw * gw + (double)gt * gt;
                }

                int bi = _bOffset[l] + n;
                sq += (double)B[bi] * B[bi] + (double)TauB[bi] * TauB[bi];
            }
        }
        return sq;
    }

    public void AddScaled(SharedGradientAccumulator src, float scale)
    {
        for (int l = 0; l < _touched.Length; l++)
        {
            bool[] st = src._touched[l];
            bool[] dt = _touched[l];
            int inputs = _inputs[l];

            for (int n = 0; n < st.Length; n++)
            {
                if (!st[n]) continue;
                dt[n] = true;

                int wi = _wOffset[l] + n * inputs;
                for (int i = 0; i < inputs; i++)
                {
                    W[wi + i]   += src.W[wi + i]   * scale;
                    Tau[wi + i] += src.Tau[wi + i] * scale;
                }

                int bi = _bOffset[l] + n;
                B[bi]    += src.B[bi]    * scale;
                TauB[bi] += src.TauB[bi] * scale;
            }
        }
        Contributions++;
    }
}