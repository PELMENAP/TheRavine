using System;
using System.Runtime.CompilerServices;

public struct RunningMeanStd
{
    private double _mean;
    private double _m2;
    private long _count;

    public float Mean => (float)_mean;
    public long Count => _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(float x)
    {
        _count++;
        double d = x - _mean;
        _mean += d / _count;
        _m2 += d * (x - _mean);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Normalize(float x, float clip)
    {
        if (_count < 2)
            return x < -clip ? -clip : (x > clip ? clip : x);

        float std = MathF.Sqrt((float)(_m2 / _count));
        if (std < 1e-6f) std = 1e-6f;

        float z = ((float)(x - _mean)) / std;
        return z < -clip ? -clip : (z > clip ? clip : z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float UpdateAndNormalize(float x, float clip)
    {
        Update(x);
        return Normalize(x, clip);
    }

    public void Reset()
    {
        _mean = 0d;
        _m2 = 0d;
        _count = 0L;
    }
}