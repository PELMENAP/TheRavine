using System;

public static class JensenShannon
{
    private const double InvLn2 = 1.4426950408889634;

    public static float Divergence(ReadOnlySpan<float> p, ReadOnlySpan<float> q)
    {
        double sp = 0d, sq = 0d;
        for (int i = 0; i < p.Length; i++)
        {
            sp += p[i];
            sq += q[i];
        }
        if (!(sp > 0d) || !(sq > 0d)) return float.NaN;

        double sum = 0d;
        for (int i = 0; i < p.Length; i++)
        {
            double a = p[i] / sp, b = q[i] / sq, m = 0.5d * (a + b);
            if (a > 0d) sum += 0.5d * a * Math.Log(a / m);
            if (b > 0d) sum += 0.5d * b * Math.Log(b / m);
        }
        return (float)(sum * InvLn2);
    }

    public static float Conditional(ReadOnlySpan<float> a, ReadOnlySpan<float> b, int rows, int columns)
    {
        double na = 0d, nb = 0d;
        for (int i = 0; i < rows * columns; i++)
        {
            na += a[i];
            nb += b[i];
        }
        if (!(na > 0d) || !(nb > 0d)) return float.NaN;

        double weighted = 0d, weights = 0d;
        for (int r = 0; r < rows; r++)
        {
            var rowA = a.Slice(r * columns, columns);
            var rowB = b.Slice(r * columns, columns);
            double ra = 0d, rb = 0d;
            for (int c = 0; c < columns; c++)
            {
                ra += rowA[c];
                rb += rowB[c];
            }
            if (!(ra > 0d) || !(rb > 0d)) continue;

            double w = 0.5d * (ra / na + rb / nb);
            weighted += w * Divergence(rowA, rowB);
            weights += w;
        }
        return weights > 0d ? (float)(weighted / weights) : float.NaN;
    }
}
