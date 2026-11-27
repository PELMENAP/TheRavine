using Unity.Mathematics;

public static class MateLoci
{
    public const int Count = 32;

    public static uint Cross(uint a, uint b, uint pick) => (a & pick) | (b & ~pick);

    public static uint Cross(uint a, uint b) => Cross(a, b, RandomMask());

    public static uint Mutate(uint loci, float chancePerLocus)
    {
        if (!(chancePerLocus > 0f)) return loci;
        for (int i = 0; i < Count; i++)
            if (RavineRandom.RangeFloat() < chancePerLocus) loci ^= 1u << i;
        return loci;
    }

    public static int2 Pair(int k)
    {
        int i = (k * 11 + 3) & (Count - 1);
        int j = (i + 1 + (k * 13) % (Count - 1)) & (Count - 1);
        return new int2(i, j);
    }

    public static int Conflicts(uint loci, int pairs)
    {
        int n = math.clamp(pairs, 0, Count * (Count - 1) / 2);
        int conflicts = 0;
        for (int k = 0; k < n; k++)
        {
            int2 p = Pair(k);
            if (((loci >> p.x) & 1u) != 0u && ((loci >> p.y) & 1u) != 0u) conflicts++;
        }
        return conflicts;
    }

    public static float Viability(int conflicts, float lethality)
        => conflicts <= 0 ? 1f : math.pow(1f - math.saturate(lethality), conflicts);

    public static float Divergence(uint[] a, int countA, uint[] b, int countB)
    {
        if (a == null || b == null || countA <= 0 || countB <= 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < Count; i++)
        {
            uint bit = 1u << i;
            int ca = 0, cb = 0;
            for (int k = 0; k < countA; k++) if ((a[k] & bit) != 0u) ca++;
            for (int k = 0; k < countB; k++) if ((b[k] & bit) != 0u) cb++;
            sum += math.abs((float)ca / countA - (float)cb / countB);
        }
        return sum / Count;
    }

    private static uint RandomMask()
        => (uint)RavineRandom.RangeInt(0, 1 << 16) | ((uint)RavineRandom.RangeInt(0, 1 << 16) << 16);
}
