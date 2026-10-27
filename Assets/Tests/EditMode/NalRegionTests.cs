using NUnit.Framework;
using TheRavine.Extensions;
using TheRavine.Generator;

public class NalRegionTests
{
    private static readonly long Center = Position2Int.Pack(10, 20);

    private static int Ring(int dx, int dz, int facing = 1, int rings = 3) =>
        MapGenerator.RingOf(Center, facing, rings, Position2Int.Pack(10 + dx, 20 + dz));

    [Test]
    public void WindowChunksAreRingZero()
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
            Assert.AreEqual(0, Ring(dx, dz));
    }

    [Test]
    public void ChunksAheadFormBlocksPerRing()
    {
        for (int dz = 2; dz <= 4; dz++) Assert.AreEqual(1, Ring(0, dz));
        for (int dz = 5; dz <= 7; dz++) Assert.AreEqual(2, Ring(0, dz));
        for (int dz = 8; dz <= 10; dz++) Assert.AreEqual(3, Ring(0, dz));
    }

    [Test]
    public void RingsWidenSideways()
    {
        Assert.AreEqual(1, Ring(4, 3));
        Assert.AreEqual(-1, Ring(5, 3));
        Assert.AreEqual(2, Ring(7, 6));
        Assert.AreEqual(-1, Ring(8, 6));
        Assert.AreEqual(1, Ring(-4, 3));
    }

    [Test]
    public void BeyondTheLastRingIsOutside()
    {
        Assert.AreEqual(-1, Ring(0, 11));
        Assert.AreEqual(-1, Ring(0, 5, rings: 1));
    }

    [Test]
    public void BehindAndBesideTheWindowIsOutside()
    {
        Assert.AreEqual(-1, Ring(0, -2));
        Assert.AreEqual(-1, Ring(2, 0));
        Assert.AreEqual(-1, Ring(0, -5));
    }

    [Test]
    public void FacingMirrorsTheRings()
    {
        Assert.AreEqual(1, Ring(0, -3, facing: -1));
        Assert.AreEqual(-1, Ring(0, 3, facing: -1));
        Assert.AreEqual(0, Ring(0, 1, facing: -1));
    }

    [Test]
    public void ZeroRingCountKeepsOnlyTheWindow()
    {
        Assert.AreEqual(0, Ring(1, 1, rings: 0));
        Assert.AreEqual(-1, Ring(0, 3, rings: 0));
    }
}
