using NUnit.Framework;
using UnityEngine;

public class ReservoirMemoryCapacityTests
{
    private const int InputSize  = 8;
    private const int Hidden     = 32;
    private const int Washout    = 200;
    private const int TrainLen   = 3000;
    private const int TestLen    = 2000;
    private const int MaxDelay   = 32;
    private const double Ridge   = 1e-4;
    private const float Radius   = 0.9f;
    private const int Reservoirs = 4;

    private SimulationRules _rules;

    [SetUp]
    public void SetUp()
    {
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(_rules);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_rules);
        SimulationRules.Bind(null);
    }

    private static double[][] Collect(LSTMMemory memory, float[] inputs)
    {
        int total = inputs.Length;
        var state = new float[Hidden * 2];
        var x = new float[InputSize];
        var states = new double[total][];
        for (int t = 0; t < total; t++)
        {
            for (int j = 0; j < InputSize; j++) x[j] = inputs[t];
            memory.StepManaged(x, state);
            var row = new double[Hidden + 1];
            for (int i = 0; i < Hidden; i++) row[i] = state[i];
            row[Hidden] = 1.0;
            states[t] = row;
        }
        return states;
    }

    private static double[] SolveRidge(double[][] states, float[] inputs, int from, int to, int delay)
    {
        int d = Hidden + 1;
        var a = new double[d, d + 1];
        for (int t = from; t < to; t++)
        {
            double target = inputs[t - delay];
            var s = states[t];
            for (int i = 0; i < d; i++)
            {
                for (int j = 0; j < d; j++) a[i, j] += s[i] * s[j];
                a[i, d] += s[i] * target;
            }
        }
        for (int i = 0; i < d; i++) a[i, i] += Ridge * (to - from);

        for (int col = 0; col < d; col++)
        {
            int pivot = col;
            for (int r = col + 1; r < d; r++)
                if (System.Math.Abs(a[r, col]) > System.Math.Abs(a[pivot, col])) pivot = r;
            if (pivot != col)
                for (int c = 0; c <= d; c++) (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);

            double inv = 1.0 / a[col, col];
            for (int r = 0; r < d; r++)
            {
                if (r == col) continue;
                double k = a[r, col] * inv;
                if (k == 0.0) continue;
                for (int c = col; c <= d; c++) a[r, c] -= k * a[col, c];
            }
        }

        var w = new double[d];
        for (int i = 0; i < d; i++) w[i] = a[i, d] / a[i, i];
        return w;
    }

    private static double Capacity(LSTMMemory memory, int seed)
    {
        var rng = new System.Random(seed);
        int total = Washout + TrainLen + TestLen;
        var inputs = new float[total];
        for (int t = 0; t < total; t++) inputs[t] = (float)(rng.NextDouble() * 2.0 - 1.0);

        var states = Collect(memory, inputs);
        int trainFrom = Washout + MaxDelay;
        int trainTo   = Washout + TrainLen;
        int testTo    = total;

        double capacity = 0.0;
        for (int k = 1; k <= MaxDelay; k++)
        {
            var w = SolveRidge(states, inputs, trainFrom, trainTo, k);

            double sp = 0, st = 0, spp = 0, stt = 0, spt = 0;
            int n = testTo - trainTo;
            for (int t = trainTo; t < testTo; t++)
            {
                double pred = 0;
                for (int i = 0; i <= Hidden; i++) pred += w[i] * states[t][i];
                double target = inputs[t - k];
                sp += pred; st += target; spp += pred * pred; stt += target * target; spt += pred * target;
            }
            double cov = spt / n - (sp / n) * (st / n);
            double vp = spp / n - (sp / n) * (sp / n);
            double vt = stt / n - (st / n) * (st / n);
            if (vp > 1e-18 && vt > 1e-18) capacity += cov * cov / (vp * vt);
        }
        return capacity;
    }

    private static double Mean(bool orthogonal)
    {
        double sum = 0.0;
        for (int r = 0; r < Reservoirs; r++)
        {
            Random.InitState(1000 + r);
            var memory = new LSTMMemory(InputSize, Hidden, Radius);
            if (orthogonal) memory.OrthogonalizeRecurrent(Radius);
            try { sum += Capacity(memory, 77 + r); }
            finally { memory.Dispose(); }
        }
        return sum / Reservoirs;
    }

    [Test]
    public void MemoryCapacityOfBothInitialisationsIsFiniteAndPositive()
    {
        double spectral   = Mean(false);
        double orthogonal = Mean(true);

        Debug.Log($"Reservoir memory capacity (hidden {Hidden}, delays 1..{MaxDelay}, radius {Radius}, mean of {Reservoirs}): spectral radius {spectral:F3}, orthogonal {orthogonal:F3}");

        Assert.IsFalse(double.IsNaN(spectral) || double.IsInfinity(spectral));
        Assert.IsFalse(double.IsNaN(orthogonal) || double.IsInfinity(orthogonal));
        Assert.Greater(spectral, 0.0);
        Assert.Greater(orthogonal, 0.0);
    }
}
