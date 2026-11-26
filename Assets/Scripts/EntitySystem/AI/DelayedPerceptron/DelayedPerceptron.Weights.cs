using System;

public partial class DelayedPerceptron
{
    public void AddWeightNoise(float relativeSigma)
    {
        if (!(relativeSigma > 0f)) return;
        SyncManaged();

        int layers = _layout.L;
        for (int l = 0; l < layers; l++)
        {
            int inputs  = _layout.LayerSizes[l];
            int neurons = _layout.LayerSizes[l + 1];
            if (inputs == 0 || neurons == 0) continue;

            double sum = 0d;
            for (int n = 0; n < neurons; n++)
            {
                int row = _layout.RowIndex(l, n);
                for (int i = 0; i < inputs; i++)
                {
                    float w = _wt[row + (i << 1)];
                    sum += w * w;
                }
            }

            float sigma = relativeSigma * (float)Math.Sqrt(sum / ((double)inputs * neurons));
            for (int n = 0; n < neurons; n++)
            {
                int row = _layout.RowIndex(l, n);
                for (int i = 0; i < inputs; i++)
                    _wt[row + (i << 1)] += sigma * SampleGaussian();
            }
        }

        _nativeStale = true;
        WeightVersion++;
    }

    public double WeightDot(DelayedPerceptron other)
    {
        if (other == null) return 0d;
        SyncManaged();
        other.SyncManaged();
        if (_wt.Length != other._wt.Length) return 0d;

        double dot = 0d;
        for (int i = 0; i < _wt.Length; i++) dot += (double)_wt[i] * other._wt[i];
        return dot;
    }

    public double WeightNormSq()
    {
        SyncManaged();
        double sum = 0d;
        for (int i = 0; i < _wt.Length; i++) sum += (double)_wt[i] * _wt[i];
        return sum;
    }
}
