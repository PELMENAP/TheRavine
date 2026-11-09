using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ViewControl;

public class NalRegionTests
{
    private static readonly long Center = Position2Int.Pack(10, 20);

    private static readonly ViewFacing[] Facings =
        { ViewFacing.PlusZ, ViewFacing.MinusZ, ViewFacing.PlusX, ViewFacing.MinusX };

    private static long Chunk(int dx, int dz) => Position2Int.Pack(10 + dx, 20 + dz);

    private static int View(int dx, int dz, ViewFacing facing = ViewFacing.PlusZ, int rings = 3) =>
        MapGenerator.ViewRingOf(Center, facing, rings, Chunk(dx, dz));

    private static int Sim(int dx, int dz, int rings = 3) =>
        MapGenerator.SimRingOf(Center, rings, Chunk(dx, dz));

    private static int2 ToWorld(ViewFacing facing, int lateral, int forward) =>
        facing.Right() * lateral + facing.Forward() * forward;

    [Test]
    public void WindowChunksAreRingZero()
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Assert.AreEqual(0, View(dx, dz));
            Assert.AreEqual(0, Sim(dx, dz));
        }
    }

    [Test]
    public void ChunksAheadFormBlocksPerRing()
    {
        for (int dz = 2; dz <= 4; dz++) Assert.AreEqual(1, View(0, dz));
        for (int dz = 5; dz <= 7; dz++) Assert.AreEqual(2, View(0, dz));
        for (int dz = 8; dz <= 10; dz++) Assert.AreEqual(3, View(0, dz));
    }

    [Test]
    public void RingsWidenSideways()
    {
        Assert.AreEqual(1, View(4, 3));
        Assert.AreEqual(-1, View(5, 3));
        Assert.AreEqual(2, View(7, 6));
        Assert.AreEqual(-1, View(8, 6));
        Assert.AreEqual(1, View(-4, 3));
    }

    [Test]
    public void BeyondTheLastRingIsOutside()
    {
        Assert.AreEqual(-1, View(0, 11));
        Assert.AreEqual(-1, View(0, 5, rings: 1));
    }

    [Test]
    public void BehindTheWindowIsOutsideOfTheView()
    {
        Assert.AreEqual(-1, View(0, -2));
        Assert.AreEqual(-1, View(0, -5));
        Assert.AreEqual(-1, View(3, -2));
    }

    [Test]
    public void SideBlocksAreRingOne()
    {
        for (int dx = 2; dx <= 4; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Assert.AreEqual(MapGenerator.SideBlockRing, View(dx, dz));
            Assert.AreEqual(MapGenerator.SideBlockRing, View(-dx, dz));
        }

        Assert.AreEqual(-1, View(5, 0));
        Assert.AreEqual(-1, View(-5, 1));
    }

    [Test]
    public void SideBlocksNeedAtLeastOneRing()
    {
        Assert.AreEqual(-1, View(3, 0, rings: 0));
        Assert.AreEqual(MapGenerator.SideBlockRing, View(3, 0, rings: 1));
    }

    [Test]
    public void FacingMirrorsTheRings()
    {
        Assert.AreEqual(1, View(0, -3, ViewFacing.MinusZ));
        Assert.AreEqual(-1, View(0, 3, ViewFacing.MinusZ));
        Assert.AreEqual(0, View(0, 1, ViewFacing.MinusZ));
    }

    [Test]
    public void SideFacingsPointAlongX()
    {
        Assert.AreEqual(1, View(3, 0, ViewFacing.PlusX));
        Assert.AreEqual(2, View(6, 0, ViewFacing.PlusX));
        Assert.AreEqual(-1, View(-3, 0, ViewFacing.PlusX));
        Assert.AreEqual(1, View(-3, 0, ViewFacing.MinusX));
        Assert.AreEqual(-1, View(3, 0, ViewFacing.MinusX));
    }

    [Test]
    public void EveryFacingIsARotationOfPlusZ()
    {
        for (int rings = 0; rings <= 4; rings++)
        foreach (ViewFacing facing in Facings)
        for (int lateral = -16; lateral <= 16; lateral++)
        for (int forward = -16; forward <= 16; forward++)
        {
            int2 d = ToWorld(facing, lateral, forward);
            Assert.AreEqual(View(lateral, forward, ViewFacing.PlusZ, rings), View(d.x, d.y, facing, rings),
                $"{facing} lateral {lateral} forward {forward} rings {rings}");
        }
    }

    [Test]
    public void ZeroRingCountKeepsOnlyTheWindow()
    {
        Assert.AreEqual(0, View(1, 1, rings: 0));
        Assert.AreEqual(-1, View(0, 3, rings: 0));
        Assert.AreEqual(0, Sim(1, 1, rings: 0));
        Assert.AreEqual(-1, Sim(0, 3, rings: 0));
    }

    [Test]
    public void SimRingsAreChebyshevShells()
    {
        for (int d = 2; d <= 4; d++) Assert.AreEqual(1, Sim(d, 0));
        for (int d = 5; d <= 7; d++) Assert.AreEqual(2, Sim(d, 0));
        for (int d = 8; d <= 10; d++) Assert.AreEqual(3, Sim(d, 0));
        Assert.AreEqual(2, Sim(5, 5));
        Assert.AreEqual(1, Sim(4, -4));
        Assert.AreEqual(-1, Sim(11, 0));
        Assert.AreEqual(-1, Sim(0, -11));
        Assert.AreEqual(-1, Sim(10, 11));
    }

    [Test]
    public void SimRingsDoNotDependOnDirection()
    {
        for (int dx = -12; dx <= 12; dx++)
        for (int dz = -12; dz <= 12; dz++)
        {
            int expected = Sim(dx, dz);
            Assert.AreEqual(expected, Sim(-dx, dz));
            Assert.AreEqual(expected, Sim(dx, -dz));
            Assert.AreEqual(expected, Sim(dz, dx));
        }
    }

    [Test]
    public void SimSquareCoversEverythingTheViewDraws()
    {
        const int rings = 4;
        foreach (ViewFacing facing in Facings)
        for (int dx = -20; dx <= 20; dx++)
        for (int dz = -20; dz <= 20; dz++)
        {
            int view = View(dx, dz, facing, rings);
            if (view < 0) continue;
            Assert.AreEqual(view, Sim(dx, dz, rings), $"{facing} {dx},{dz}");
        }
    }

    [Test]
    public void SimSquareHoldsEveryChunkOfTheLastRing()
    {
        const int rings = 4;
        int count = 0;
        for (int dx = -20; dx <= 20; dx++)
        for (int dz = -20; dz <= 20; dz++)
            if (Sim(dx, dz, rings) >= 0) count++;

        int side = 2 * (rings * MapGenerator.WindowSide + MapGenerator.chunkScale) + 1;
        Assert.AreEqual(side * side, count);
    }
}
