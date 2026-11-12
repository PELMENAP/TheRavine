using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Generator;

public class FireMathTests
{
    private static FireRules Rules() => new()
    {
        defaultSize = 1f,
        minSize = 0.25f,
        maxSize = 8f,
        burnSeconds = 6f,
        burnSizeExponent = 0.5f,
        heatRadiusPerSize = 0.5f,
        spreadReachPerSize = 1.5f,
        spreadMaxReach = 6f,
        spreadAttemptsPerSize = 8f,
        spreadMaxAttempts = 32,
    };

    [Test]
    public void SizeIsShapeHeightOrDefault()
    {
        FireRules r = Rules();
        Assert.AreEqual(1f, FireMath.Size(default, r), 1e-6f);
        Assert.AreEqual(3f, FireMath.Size(CollisionShape.Cylinder(0.5f, 3f), r), 1e-6f);
        Assert.AreEqual(2f, FireMath.Size(CollisionShape.Box(new float2(1f, 1f), 2f), r), 1e-6f);
        Assert.AreEqual(8f, FireMath.Size(CollisionShape.Cylinder(0.5f, 40f), r), 1e-6f);
        Assert.AreEqual(0.25f, FireMath.Size(CollisionShape.Cylinder(0.5f, 0.01f), r), 1e-6f);
    }

    [Test]
    public void LargerFiresBurnLongerAndSpreadFurther()
    {
        FireRules r = Rules();
        Assert.AreEqual(6f, FireMath.BurnSeconds(1f, r), 1e-5f);
        Assert.AreEqual(12f, FireMath.BurnSeconds(4f, r), 1e-4f);

        Assert.AreEqual(1.5f, FireMath.SpreadReach(1f, r), 1e-6f);
        Assert.AreEqual(1f, FireMath.SpreadReach(0.25f, r), 1e-6f);
        Assert.AreEqual(6f, FireMath.SpreadReach(8f, r), 1e-6f);

        Assert.AreEqual(8, FireMath.SpreadAttempts(1f, r));
        Assert.AreEqual(2, FireMath.SpreadAttempts(0.25f, r));
        Assert.AreEqual(32, FireMath.SpreadAttempts(8f, r));

        Assert.Greater(FireMath.HeatRadius(4f, r), FireMath.HeatRadius(1f, r));
    }

    [Test]
    public void FalloffAndCoverShrinkWithDistance()
    {
        Assert.Greater(FireMath.SpreadFalloff(1f, 3f), FireMath.SpreadFalloff(3f, 3f));
        Assert.AreEqual(0f, FireMath.SpreadFalloff(10f, 3f), 1e-6f);

        Assert.AreEqual(1f, FireMath.HeatCover(0f, 2f), 1e-6f);
        Assert.AreEqual(0.5f, FireMath.HeatCover(1.5f, 2f), 1e-6f);
        Assert.AreEqual(0f, FireMath.HeatCover(2.5f, 2f), 1e-6f);
    }

    [Test]
    public void WinterStopsIgnition()
    {
        Assert.AreEqual(0f, FireMath.SeasonFactor(1f, true, 0.5f), 1e-6f);
        Assert.AreEqual(1.5f, FireMath.SeasonFactor(1f, false, 0.5f), 1e-6f);
        Assert.AreEqual(0.5f, FireMath.SeasonFactor(-1f, false, 0.5f), 1e-6f);
    }
}
