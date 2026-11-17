using NUnit.Framework;
using UnityEngine;

public class ObjectGrowthMathTests
{
    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-4f);
        Assert.AreEqual(expected.y, actual.y, 1e-4f);
        Assert.AreEqual(expected.z, actual.z, 1e-4f);
    }

    [Test]
    public void MeasuredSizesGiveAPerAxisRatio()
    {
        Vector3 ratio = ObjectGrowthMath.Resolve(new Vector3(1f, 2f, 1f), new Vector3(2f, 6f, 4f), 1f, 1f);

        AssertVector(new Vector3(2f, 3f, 4f), ratio);
    }

    [Test]
    public void MissingSizesFallBackToTheHeightRatio()
    {
        Vector3 ratio = ObjectGrowthMath.Resolve(Vector3.zero, new Vector3(2f, 6f, 4f), 2f, 6f);

        AssertVector(Vector3.one * 3f, ratio);
    }

    [Test]
    public void NothingMeasurableGivesUnitRatio()
    {
        AssertVector(Vector3.one, ObjectGrowthMath.Resolve(Vector3.zero, Vector3.zero, 0f, 3f));
        AssertVector(Vector3.one, ObjectGrowthMath.Resolve(Vector3.zero, Vector3.zero, 2f, 0f));
    }

    [Test]
    public void ZeroOnOneAxisRejectsTheMeasurement()
    {
        Vector3 ratio = ObjectGrowthMath.Resolve(new Vector3(1f, 0f, 1f), new Vector3(2f, 2f, 2f), 1f, 2f);

        AssertVector(Vector3.one * 2f, ratio);
    }

    [Test]
    public void RatioIsClamped()
    {
        Vector3 huge = ObjectGrowthMath.Resolve(new Vector3(0.01f, 0.01f, 0.01f), new Vector3(100f, 100f, 100f), 1f, 1f);
        Vector3 tiny = ObjectGrowthMath.Resolve(new Vector3(100f, 100f, 100f), new Vector3(0.01f, 0.01f, 0.01f), 1f, 1f);

        AssertVector(Vector3.one * ObjectGrowthMath.MaxRatio, huge);
        AssertVector(Vector3.one * ObjectGrowthMath.MinRatio, tiny);
    }

    [Test]
    public void SizeAtBaseUndoesTheCurrentScale()
    {
        Vector3 size = ObjectGrowthMath.SizeAtBase(new Vector3(1f, 2f, 3f), new Vector3(0.5f, 0.5f, 0.5f), Vector3.one);

        AssertVector(new Vector3(2f, 4f, 6f), size);
    }

    [Test]
    public void SizeAtBaseIgnoresAZeroScale()
    {
        Vector3 size = ObjectGrowthMath.SizeAtBase(new Vector3(1f, 2f, 3f), Vector3.zero, Vector3.one);

        AssertVector(new Vector3(1f, 2f, 3f), size);
    }

    [Test]
    public void FractionScalesTheRatioButNeverShrinks()
    {
        AssertVector(new Vector3(1.6f, 2.4f, 1f), ObjectGrowthMath.ApplyFraction(new Vector3(2f, 3f, 0.5f), 0.8f));
        AssertVector(new Vector3(2f, 3f, 1f), ObjectGrowthMath.ApplyFraction(new Vector3(2f, 3f, 0.5f), 1f));
    }

    [Test]
    public void ScaleRunsFromBaseToBaseTimesRatio()
    {
        var baseScale = new Vector3(1f, 2f, 1f);
        var ratio = new Vector3(3f, 2f, 1f);

        AssertVector(baseScale, ObjectGrowthMath.ScaleAt(baseScale, ratio, 0f));
        AssertVector(new Vector3(3f, 4f, 1f), ObjectGrowthMath.ScaleAt(baseScale, ratio, 1f));
        AssertVector(new Vector3(2f, 3f, 1f), ObjectGrowthMath.ScaleAt(baseScale, ratio, 0.5f));
    }
}
