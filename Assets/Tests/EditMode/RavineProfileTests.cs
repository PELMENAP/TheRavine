using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Generator;

public class RavineProfileTests
{
    private const float HalfWidth = 10000f;
    private const float Run = 100f;
    private const float Height = 5000f;

    private static RavineProfile Profile() => new(HalfWidth, Run, Height);

    [Test]
    public void ProfileIsSymmetricInZ()
    {
        RavineProfile p = Profile();
        float[] zs = { 0f, 5000f, HalfWidth, HalfWidth + 1f, HalfWidth + 37.5f, HalfWidth + Run, HalfWidth + Run + 500f };
        foreach (float z in zs)
        {
            Assert.AreEqual(p.Blend(z), p.Blend(-z), 1e-6f);
            Assert.AreEqual(p.Elevation(z), p.Elevation(-z), 1e-3f);
        }
    }

    [Test]
    public void ValleyHasNoElevation()
    {
        RavineProfile p = Profile();
        Assert.AreEqual(0f, p.Elevation(0f));
        Assert.AreEqual(0f, p.Elevation(HalfWidth * 0.5f));
        Assert.AreEqual(0f, p.Elevation(HalfWidth));
        Assert.IsTrue(RavineProfile.IsValley(p.Blend(HalfWidth - 1f)));
        Assert.IsFalse(RavineProfile.IsWall(p.Blend(HalfWidth - 1f)));
    }

    [Test]
    public void PlateauHasFullHeight()
    {
        RavineProfile p = Profile();
        Assert.AreEqual(Height, p.Elevation(HalfWidth + Run), 1e-2f);
        Assert.AreEqual(Height, p.Elevation(HalfWidth + Run + 1f), 1e-2f);
        Assert.AreEqual(Height, p.Elevation(-(HalfWidth + Run * 30f)), 1e-2f);
        Assert.IsTrue(RavineProfile.IsPlateau(p.Blend(HalfWidth + Run + 1f)));
        Assert.IsFalse(RavineProfile.IsWall(p.Blend(HalfWidth + Run + 1f)));
    }

    [Test]
    public void WallSlopeIsHeightOverRun()
    {
        RavineProfile p = Profile();
        float slope = Height / Run;
        for (float z = HalfWidth + 10f; z < HalfWidth + Run - 10f; z += 10f)
        {
            Assert.IsTrue(RavineProfile.IsWall(p.Blend(z)));
            float dz = 2f;
            Assert.AreEqual(slope, (p.Elevation(z + dz) - p.Elevation(z)) / dz, 0.05f);
            Assert.AreEqual(-slope, (p.Elevation(-z - dz) - p.Elevation(-z)) / dz, 0.05f);
        }
    }

    [Test]
    public void ZeroHeightIsFlatEverywhere()
    {
        var p = new RavineProfile(HalfWidth, Run, 0f);
        float[] zs = { 0f, HalfWidth, HalfWidth + Run * 0.5f, HalfWidth + Run, -HalfWidth - Run * 10f, 1e6f };
        foreach (float z in zs)
        {
            Assert.AreEqual(0f, p.Elevation(z));
            Assert.AreEqual(0f, p.Blend(z));
            Assert.IsTrue(RavineProfile.IsValley(p.Blend(z)));
        }
        Assert.AreEqual(float2.zero, p.ElevationRange(-1e6f, 1e6f));
    }

    [Test]
    public void ElevationRangeCoversBlock()
    {
        RavineProfile p = Profile();
        Assert.AreEqual(new float2(0f, 0f), p.ElevationRange(-500f, 500f));
        float2 wall = p.ElevationRange(HalfWidth - 100f, HalfWidth + 300f);
        Assert.AreEqual(0f, wall.x, 1e-3f);
        Assert.AreEqual(Height, wall.y, 1e-2f);
        float2 across = p.ElevationRange(-HalfWidth - 300f, HalfWidth + 300f);
        Assert.AreEqual(0f, across.x, 1e-3f);
        Assert.AreEqual(Height, across.y, 1e-2f);
        float2 plateau = p.ElevationRange(HalfWidth + Run, HalfWidth + Run + 384f);
        Assert.AreEqual(plateau.x, plateau.y, 1e-2f);
    }
}
