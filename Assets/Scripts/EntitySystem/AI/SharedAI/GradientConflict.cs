using Unity.Mathematics;

public sealed class GradientConflict
{
    public const int Groups = (int)Caste.Count;
    public const int Pairs  = Groups * (Groups - 1) / 2;

    public readonly double[] Dot   = new double[Groups * Groups];
    public readonly double[] Sq    = new double[Groups];
    public readonly long[]   Count = new long[Groups];

    public readonly float[] Cos  = new float[Pairs];
    public readonly float[] Norm = new float[Groups];
    public bool Fresh { get; private set; }

    public static int PairIndex(int a, int b)
    {
        int row = a * Groups - a * (a + 1) / 2;
        return row + (b - a - 1);
    }

    public void Begin()
    {
        System.Array.Clear(Dot, 0, Dot.Length);
        System.Array.Clear(Sq, 0, Sq.Length);
        System.Array.Clear(Count, 0, Count.Length);
    }

    public void Finish()
    {
        bool any = false;
        for (int a = 0; a < Groups; a++)
        {
            bool has = Count[a] > 0 && Sq[a] > 0d;
            Norm[a] = has ? (float)(math.sqrt(Sq[a]) / Count[a]) : float.NaN;
            any |= has;
            for (int b = a + 1; b < Groups; b++)
            {
                bool both = has && Count[b] > 0 && Sq[b] > 0d;
                Cos[PairIndex(a, b)] = both ? (float)(Dot[a * Groups + b] / math.sqrt(Sq[a] * Sq[b])) : float.NaN;
            }
        }
        Fresh = any;
    }

    public void Consume() => Fresh = false;
}
