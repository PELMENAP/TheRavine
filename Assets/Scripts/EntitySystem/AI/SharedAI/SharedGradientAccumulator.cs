using System;

public sealed class SharedGradientAccumulator
{
    private readonly PerceptronLayout _lay;
    private readonly bool[][] _touched;

    public readonly float[] WT;
    public readonly float[] BT;

    public int Contributions { get; private set; }

    public SharedGradientAccumulator(PerceptronLayout layout)
    {
        _lay = layout;
        _touched = new bool[layout.L][];
        for (int l = 0; l < layout.L; l++)
            _touched[l] = new bool[layout.LayerSizes[l + 1]];

        WT = new float[layout.WeightTotal];
        BT = new float[layout.BiasTotal];
    }

    public int Neurons(int l) => _lay.LayerSizes[l + 1];
    public int Inputs(int l)  => _lay.LayerSizes[l];
    public int RowIndex(int l, int n)  => _lay.RowIndex(l, n);
    public int BiasIndex(int l, int n) => _lay.BiasIndex(l, n);

    public bool IsTouched(int l, int n)   => _touched[l][n];
    public void MarkTouched(int l, int n) => _touched[l][n] = true;

    public void Clear()
    {
        for (int l = 0; l < _touched.Length; l++)
        {
            bool[] t = _touched[l];
            int span = _lay.LayerSizes[l] << 1;

            for (int n = 0; n < t.Length; n++)
            {
                if (!t[n]) continue;
                t[n] = false;

                Array.Clear(WT, _lay.RowIndex(l, n), span);

                int bi = _lay.BiasIndex(l, n);
                BT[bi]     = 0f;
                BT[bi | 1] = 0f;
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
            int span = _lay.LayerSizes[l] << 1;

            for (int n = 0; n < t.Length; n++)
            {
                if (!t[n]) continue;

                int wi = _lay.RowIndex(l, n);
                for (int i = 0; i < span; i++)
                {
                    float g = WT[wi + i];
                    sq += (double)g * g;
                }

                int bi = _lay.BiasIndex(l, n);
                sq += (double)BT[bi] * BT[bi] + (double)BT[bi | 1] * BT[bi | 1];
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
            int span = _lay.LayerSizes[l] << 1;

            for (int n = 0; n < st.Length; n++)
            {
                if (!st[n]) continue;
                dt[n] = true;

                int wi = _lay.RowIndex(l, n);
                for (int i = 0; i < span; i++)
                    WT[wi + i] += src.WT[wi + i] * scale;

                int bi = _lay.BiasIndex(l, n);
                BT[bi]     += src.BT[bi]     * scale;
                BT[bi | 1] += src.BT[bi | 1] * scale;
            }
        }
        Contributions++;
    }
}