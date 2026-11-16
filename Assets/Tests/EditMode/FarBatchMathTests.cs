using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Generator.EndlessGenerators;

public class FarBatchMathTests
{
    private static readonly float2 Center = new float2(100f, 100f);
    private const float DistanceSq = 50f * 50f;

    [Test]
    public void SingleStrideIgnoresDistance()
    {
        int near = FarBatchMath.BatchIndex(5, 2, 3, 1, 1, new float2(100f, 100f), Center, DistanceSq);
        int far = FarBatchMath.BatchIndex(5, 2, 3, 1, 1, new float2(9000f, 100f), Center, DistanceSq);

        Assert.AreEqual(12, near);
        Assert.AreEqual(12, far);
    }

    [Test]
    public void DoubleStrideSplitsByDistance()
    {
        int near = FarBatchMath.BatchIndex(5, 2, 3, 1, 2, new float2(120f, 100f), Center, DistanceSq);
        int far = FarBatchMath.BatchIndex(5, 2, 3, 1, 2, new float2(200f, 100f), Center, DistanceSq);

        Assert.AreEqual(19, near);
        Assert.AreEqual(20, far);
    }

    [Test]
    public void InstanceExactlyAtTheDistanceStillCasts()
    {
        int index = FarBatchMath.BatchIndex(0, 0, 1, 0, 2, new float2(150f, 100f), Center, DistanceSq);

        Assert.AreEqual(0, index);
    }

    [Test]
    public void EveryVariantSlotAndSideGetsItsOwnBatch()
    {
        var seen = new HashSet<int>();
        for (int v = 0; v < 3; v++)
        for (int s = 0; s < 3; s++)
        {
            seen.Add(FarBatchMath.BatchIndex(7, v, 3, s, 2, Center, Center, DistanceSq));
            seen.Add(FarBatchMath.BatchIndex(7, v, 3, s, 2, new float2(1000f, 1000f), Center, DistanceSq));
        }

        Assert.AreEqual(18, seen.Count);
        foreach (int index in seen)
            Assert.That(index, Is.InRange(7, 24));
    }
}
