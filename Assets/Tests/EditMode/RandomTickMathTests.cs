using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Generator;

public class RandomTickMathTests
{
    [Test]
    public void FarRingsSampleFewerCells()
    {
        Assert.AreEqual(16f, RandomTickMath.CellsPerTick(16f, 0, 8f), 1e-6f);
        Assert.AreEqual(2f, RandomTickMath.CellsPerTick(16f, 1, 8f), 1e-6f);
        Assert.AreEqual(1f, RandomTickMath.CellsPerTick(16f, 2, 8f), 1e-6f);
    }

    [Test]
    public void MeanHitSecondsIsCellsOverRate()
    {
        Assert.AreEqual(256f, RandomTickMath.MeanHitSeconds(4096, 16f, 1f, 0, 8f), 1e-3f);
        Assert.AreEqual(512f, RandomTickMath.MeanHitSeconds(4096, 16f, 2f, 0, 8f), 1e-3f);
        Assert.AreEqual(2048f, RandomTickMath.MeanHitSeconds(4096, 16f, 1f, 1, 8f), 1e-3f);
        Assert.AreEqual(4096f, RandomTickMath.MeanHitSeconds(4096, 16f, 1f, 2, 8f), 1e-3f);
    }

    [Test]
    public void NoCellsMeansNoHits()
    {
        Assert.IsTrue(float.IsPositiveInfinity(RandomTickMath.MeanHitSeconds(4096, 0f, 1f, 0, 8f)));
    }

    [Test]
    public void WindowRateDoesNotDependOnObjects()
    {
        float perSecond = 9f * RandomTickMath.CellsPerTick(16f, 0, 8f) / 1f;
        Assert.AreEqual(144f, perSecond, 1e-4f);
    }

    [Test]
    public void NalAcceptChanceScalesInverselyWithDelay()
    {
        Assert.AreEqual(1f, RandomTickMath.NalAcceptChance(10, 10f), 1e-6f);
        Assert.AreEqual(1f / 6f, RandomTickMath.NalAcceptChance(60, 10f), 1e-6f);
        Assert.AreEqual(0.5f, RandomTickMath.NalAcceptChance(20, 10f), 1e-6f);
    }

    [Test]
    public void NalAcceptChanceIsClamped()
    {
        Assert.AreEqual(1f, RandomTickMath.NalAcceptChance(5, 10f), 1e-6f);
        Assert.AreEqual(1f, RandomTickMath.NalAcceptChance(0, 10f), 1e-6f);
        Assert.AreEqual(0f, RandomTickMath.NalAcceptChance(10, 0f), 1e-6f);
    }

    [Test]
    public void DecayChanceFollowsExponentialLifetime()
    {
        Assert.AreEqual(1f - math.exp(-1f), RandomTickMath.DecayChance(256f, 256f), 1e-5f);
        Assert.AreEqual(0f, RandomTickMath.DecayChance(0f, 256f), 1e-6f);
        Assert.Less(RandomTickMath.DecayChance(10f, 600f), RandomTickMath.DecayChance(10f, 60f));
    }

    [Test]
    public void DecayWithoutLifetimeIsImmediate()
    {
        Assert.AreEqual(1f, RandomTickMath.DecayChance(256f, 0f), 1e-6f);
    }

    [Test]
    public void TwoHitsSurviveLikeOneHitOverDoubleTime()
    {
        float one = RandomTickMath.DecayChance(256f, 300f);
        float two = 1f - (1f - one) * (1f - one);
        Assert.AreEqual(RandomTickMath.DecayChance(512f, 300f), two, 1e-5f);
    }
}
