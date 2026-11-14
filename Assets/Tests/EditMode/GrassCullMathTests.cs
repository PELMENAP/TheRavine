using NUnit.Framework;
using Unity.Mathematics;

public class GrassCullMathTests
{
    [Test]
    public void RectCoversTheWholeWindowWhenReachIsLarge()
    {
        bool any = GrassCullMath.CellRect(new float2(0f, 0f), 1000f, -192, -192, 384, 384, out int x, out int z, out int w, out int h);

        Assert.IsTrue(any);
        Assert.AreEqual(-192, x);
        Assert.AreEqual(-192, z);
        Assert.AreEqual(384, w);
        Assert.AreEqual(384, h);
    }

    [Test]
    public void RectShrinksToTheReach()
    {
        bool any = GrassCullMath.CellRect(new float2(0f, 0f), 50f, -192, -192, 384, 384, out int x, out int z, out int w, out int h);

        Assert.IsTrue(any);
        Assert.AreEqual(-50, x);
        Assert.AreEqual(-50, z);
        Assert.AreEqual(100, w);
        Assert.AreEqual(100, h);
    }

    [Test]
    public void RectKeepsPartialOverlapAtTheWindowEdge()
    {
        bool any = GrassCullMath.CellRect(new float2(180f, 0f), 40f, -192, -192, 384, 384, out int x, out int z, out int w, out int h);

        Assert.IsTrue(any);
        Assert.AreEqual(140, x);
        Assert.AreEqual(52, w);
        Assert.AreEqual(80, h);
    }

    [Test]
    public void RectOutsideTheWindowIsEmpty()
    {
        bool any = GrassCullMath.CellRect(new float2(1000f, 0f), 100f, -192, -192, 384, 384, out int x, out int z, out int w, out int h);

        Assert.IsFalse(any);
        Assert.AreEqual(0, w);
    }
}
