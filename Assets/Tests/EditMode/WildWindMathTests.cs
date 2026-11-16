using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Generator;

public class WildWindMathTests
{
    private const float Bottom = 3000f;
    private const float Top = 4000f;
    private const float Fade = 150f;

    [Test]
    public void BandEdgesAreInclusive()
    {
        Assert.IsTrue(WildWindMath.InBand(Bottom, Bottom, Top));
        Assert.IsTrue(WildWindMath.InBand(Top, Bottom, Top));
        Assert.IsTrue(WildWindMath.InBand(3500f, Bottom, Top));
        Assert.IsFalse(WildWindMath.InBand(Bottom - 0.01f, Bottom, Top));
        Assert.IsFalse(WildWindMath.InBand(Top + 0.01f, Bottom, Top));
    }

    [Test]
    public void FadeRisesOverFadeWidthAtBothEdges()
    {
        Assert.AreEqual(0f, WildWindMath.BandFade(Bottom, Bottom, Top, Fade), 1e-6f);
        Assert.AreEqual(0f, WildWindMath.BandFade(Top, Bottom, Top, Fade), 1e-6f);
        Assert.AreEqual(0.5f, WildWindMath.BandFade(Bottom + Fade * 0.5f, Bottom, Top, Fade), 1e-5f);
        Assert.AreEqual(0.5f, WildWindMath.BandFade(Top - Fade * 0.5f, Bottom, Top, Fade), 1e-5f);
        Assert.AreEqual(1f, WildWindMath.BandFade(Bottom + Fade, Bottom, Top, Fade), 1e-6f);
        Assert.AreEqual(1f, WildWindMath.BandFade(3500f, Bottom, Top, Fade), 1e-6f);
        Assert.AreEqual(1f, WildWindMath.BandFade(Bottom, Bottom, Top, 0f), 1e-6f);
    }

    [Test]
    public void NothingOutsideTheBand()
    {
        Assert.AreEqual(0f, WildWindMath.BandFade(Bottom - 1f, Bottom, Top, Fade));
        Assert.AreEqual(0f, WildWindMath.BandFade(Top + 1f, Bottom, Top, Fade));
        Assert.AreEqual(0f, WildWindMath.BandFade(0f, Bottom, Top, 0f));
        float fade = WildWindMath.BandFade(Top + 1f, Bottom, Top, Fade);
        Assert.AreEqual(0f, WildWindMath.GustDamage(0f, 40f, 15f, fade));
    }

    [Test]
    public void DamageFallsLinearlyToTheGustRadius()
    {
        Assert.AreEqual(1f, WildWindMath.Falloff(0f, 40f), 1e-6f);
        Assert.AreEqual(0.5f, WildWindMath.Falloff(20f, 40f), 1e-6f);
        Assert.AreEqual(0f, WildWindMath.Falloff(40f, 40f), 1e-6f);
        Assert.AreEqual(0f, WildWindMath.Falloff(100f, 40f), 1e-6f);
        Assert.AreEqual(0f, WildWindMath.Falloff(0f, 0f), 1e-6f);

        Assert.AreEqual(15f, WildWindMath.GustDamage(0f, 40f, 15f, 1f), 1e-5f);
        Assert.AreEqual(3.75f, WildWindMath.GustDamage(20f, 40f, 15f, 0.5f), 1e-5f);
        Assert.AreEqual(0f, WildWindMath.GustDamage(41f, 40f, 15f, 1f), 1e-6f);
    }

    [Test]
    public void ActiveOnlyWhenTheSquareReachesTheWall()
    {
        Assert.IsFalse(WildWindMath.Reaches(0f, 2000f, 10000f));
        Assert.IsTrue(WildWindMath.Reaches(8500f, 2000f, 10000f));
        Assert.IsTrue(WildWindMath.Reaches(-8500f, 2000f, 10000f));
    }

    [Test]
    public void GustsSitOnTheWallAndWrapAroundTheSquare()
    {
        var ravine = new RavineProfile(10000f, 100f, 5000f);
        Assert.AreEqual(10070f, WildWindMath.WallZ(in ravine, 3500f, 1f), 1e-2f);
        Assert.AreEqual(-10070f, WildWindMath.WallZ(in ravine, 3500f, -1f), 1e-2f);
        Assert.AreEqual(10100f, WildWindMath.WallZ(in ravine, 6000f, 1f), 1e-2f);

        Assert.AreEqual(-900f, WildWindMath.WrapX(1100f, 0f, 1000f), 1e-3f);
        Assert.AreEqual(900f, WildWindMath.WrapX(-1100f, 0f, 1000f), 1e-3f);
        Assert.AreEqual(500f, WildWindMath.WrapX(500f, 0f, 1000f), 1e-3f);

        float3 step = WildWindMath.StepToward(float3.zero, new float3(100f, 0f, 0f), 30f, 1f);
        Assert.AreEqual(30f, step.x, 1e-4f);
        float3 arrive = WildWindMath.StepToward(float3.zero, new float3(10f, 0f, 0f), 30f, 1f);
        Assert.AreEqual(10f, arrive.x, 1e-4f);
    }
}
