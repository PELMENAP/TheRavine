using System;
using NUnit.Framework;

public class RunningMeanStdTests
{
    [Test]
    public void MatchesNaiveMeanAndStd()
    {
        var norm = new RunningMeanStd();
        double sum = 0d, sq = 0d;
        const int n = 500;
        for (int i = 0; i < n; i++)
        {
            float x = MathF.Sin(i * 0.7f) * 3f + 0.5f;
            norm.Update(x);
            sum += x;
        }

        double mean = sum / n;
        for (int i = 0; i < n; i++)
        {
            double d = MathF.Sin(i * 0.7f) * 3f + 0.5f - mean;
            sq += d * d;
        }

        Assert.AreEqual(n, norm.Count);
        Assert.AreEqual((float)mean, norm.Mean, 1e-4f);
        Assert.AreEqual((float)Math.Sqrt(sq / n), norm.Std, 1e-4f);
    }

    [Test]
    public void ScaleDividesByStdWithoutCentering()
    {
        var norm = new RunningMeanStd();
        for (int i = 0; i < 1000; i++) norm.Update((i & 1) == 0 ? 2f : 6f);

        Assert.AreEqual(4f, norm.Mean, 1e-4f);
        Assert.AreEqual(2f, norm.Std, 1e-4f);
        Assert.AreEqual(1.5f, norm.Scale(3f, 10f, 0.05f), 1e-4f);
        Assert.AreEqual(-1.5f, norm.Scale(-3f, 10f, 0.05f), 1e-4f);
    }

    [Test]
    public void ScaleClipsAndRespectsFloor()
    {
        var norm = new RunningMeanStd();
        for (int i = 0; i < 100; i++) norm.Update(1f);

        Assert.AreEqual(0f, norm.Std, 1e-6f);
        Assert.AreEqual(0.5f / 0.25f, norm.Scale(0.5f, 10f, 0.25f), 1e-5f);
        Assert.AreEqual(5f, norm.Scale(100f, 5f, 0.25f));
        Assert.AreEqual(-5f, norm.Scale(-100f, 5f, 0.25f));
    }

    [Test]
    public void ScaleIsIdentityBeforeTwoSamples()
    {
        var norm = new RunningMeanStd();
        Assert.AreEqual(1f, norm.Std);
        Assert.AreEqual(-0.7f, norm.Scale(-0.7f, 5f, 0.05f));
        norm.Update(3f);
        Assert.AreEqual(-0.7f, norm.Scale(-0.7f, 5f, 0.05f));
    }

    [Test]
    public void ResetClearsStatistics()
    {
        var norm = new RunningMeanStd();
        for (int i = 0; i < 10; i++) norm.Update(i);
        norm.Reset();
        Assert.AreEqual(0, norm.Count);
        Assert.AreEqual(0f, norm.Mean);
        Assert.AreEqual(1f, norm.Std);
    }

    [Test]
    public void UpdateAndScaleDoesNotAllocate()
    {
        var norm = new RunningMeanStd();
        float x = 0f;
        AllocationAssert.Zero(() =>
        {
            for (int i = 0; i < 64; i++) x += norm.UpdateAndScale(i * 0.1f - 3f, 5f, 0.05f);
        }, 100, "RunningMeanStd.UpdateAndScale");
    }
}
