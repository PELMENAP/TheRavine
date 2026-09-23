using System;
using System.Runtime.CompilerServices;

public struct RunningMeanStd
{
    private double _mean;
    private double _m2;
    private long _count;

    public float Mean => (float)_mean;
    public long Count => _count;
    public float Std => _count < 2 ? 1f : (float)Math.Sqrt(_m2 / _count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(float x)
    {
        _count++;
        double d = x - _mean;
        _mean += d / _count;
        _m2 += d * (x - _mean);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Scale(float x, float clip, float stdFloor)
    {
        float z = x;
        if (_count >= 2)
        {
            float std = (float)Math.Sqrt(_m2 / _count);
            z = x / MathF.Max(std, MathF.Max(stdFloor, 1e-6f));
        }
        return z < -clip ? -clip : (z > clip ? clip : z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float UpdateAndScale(float x, float clip, float stdFloor)
    {
        Update(x);
        return Scale(x, clip, stdFloor);
    }

    public void Reset()
    {
        _mean = 0d;
        _m2 = 0d;
        _count = 0L;
    }
}