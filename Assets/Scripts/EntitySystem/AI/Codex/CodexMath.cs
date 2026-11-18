using Unity.Mathematics;
using System;

public static class CodexMath
{
    private const int AxisCheck = 1 / ((int)VarianceAxis.Count <= LawRecord.Capacity ? 1 : 0);

    public static void Aggregate(ReadOnlySpan<LawRecord> laws, ReadOnlySpan<float> opposition, float inertia,
        ReadOnlySpan<float> min, ReadOnlySpan<float> max, Span<float> theta)
    {
        int axes = math.min(theta.Length, LawRecord.Capacity);
        Span<float> den = stackalloc float[LawRecord.Capacity];
        float floor = math.max(inertia, 1e-6f);

        for (int a = 0; a < axes; a++)
        {
            theta[a] = 0f;
            float o = a < opposition.Length ? opposition[a] : 0f;
            den[a] = floor + math.select(0f, o, o > 0f && math.isfinite(o));
        }

        for (int j = 0; j < laws.Length; j++)
        {
            ref readonly var law = ref laws[j];
            float s = law.Support;
            if (!law.IsAlive || !(s > 0f) || !math.isfinite(s)) continue;

            var delta = LawRecord.DeltaReadOnly(in law);
            for (int a = 0; a < axes; a++)
            {
                float d = delta[a];
                if (d == 0f) continue;
                theta[a] += s * d;
                den[a] += s;
            }
        }

        for (int a = 0; a < axes; a++)
        {
            float v = theta[a] / den[a];
            if (!math.isfinite(v)) v = 0f;
            float lo = a < min.Length ? min[a] : 0f;
            float hi = a < max.Length ? max[a] : 0f;
            theta[a] = math.clamp(v, lo, hi);
        }
    }

    public static float Cost(ReadOnlySpan<float> delta, ReadOnlySpan<float> price)
    {
        int n = math.min(delta.Length, price.Length);
        float sum = 0f;
        for (int k = 0; k < n; k++) sum += price[k] * delta[k];
        return sum;
    }

    public static float VoteGain(float energy, float scale)
        => scale * math.sqrt(math.max(energy, 0f));

    public static float Decay(float support, float rate, float dt)
        => support * math.exp(-math.max(rate, 0f) * math.max(dt, 0f));

    public static void Multipliers(ReadOnlySpan<float> theta, Span<float> multipliers)
    {
        int n = math.min(theta.Length, multipliers.Length);
        for (int k = 0; k < n; k++) multipliers[k] = math.exp(theta[k]);
    }
}
