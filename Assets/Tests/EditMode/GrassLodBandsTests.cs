using NUnit.Framework;
using TheRavine.Generator;
using TheRavine.ViewControl;

public class GrassLodBandsTests
{
    private const int Window = 384;

    [Test]
    public void StepOneAndBelowIsFullDetail()
    {
        Assert.AreEqual(1, GrassLodBands.ValidStep(1, Window));
        Assert.AreEqual(1, GrassLodBands.ValidStep(0, Window));
        Assert.AreEqual(1, GrassLodBands.ValidStep(-7, Window));
        Assert.AreEqual(1, GrassLodBands.ValidStep(4, 0));
    }

    [Test]
    public void StepIsRaisedToAPowerOfTwo()
    {
        Assert.AreEqual(2, GrassLodBands.ValidStep(2, Window));
        Assert.AreEqual(4, GrassLodBands.ValidStep(3, Window));
        Assert.AreEqual(4, GrassLodBands.ValidStep(4, Window));
        Assert.AreEqual(8, GrassLodBands.ValidStep(5, Window));
    }

    [Test]
    public void StepNeverExceedsTheLargestPowerOfTwoDividingTheWindow()
    {
        Assert.AreEqual(128, GrassLodBands.ValidStep(1000, Window));
        Assert.AreEqual(32, GrassLodBands.ValidStep(64, 96));
        for (int step = 1; step <= 600; step++)
            Assert.AreEqual(0, Window % GrassLodBands.ValidStep(step, Window), $"step {step}");
    }

    [Test]
    public void WidthScaleIsClampedAndNanSafe()
    {
        Assert.AreEqual(GrassLodBands.MinWidthScale, GrassLodBands.ValidWidthScale(float.NaN));
        Assert.AreEqual(GrassLodBands.MinWidthScale, GrassLodBands.ValidWidthScale(-3f));
        Assert.AreEqual(GrassLodBands.MaxWidthScale, GrassLodBands.ValidWidthScale(1000f));
        Assert.AreEqual(GrassLodBands.MaxWidthScale, GrassLodBands.ValidWidthScale(float.PositiveInfinity));
        Assert.AreEqual(2f, GrassLodBands.ValidWidthScale(2f));
    }

    [Test]
    public void CellsPerSideFollowsTheValidStep()
    {
        Assert.AreEqual(384, GrassLodBands.CellsPerSide(Window, 1));
        Assert.AreEqual(96, GrassLodBands.CellsPerSide(Window, 4));
        Assert.AreEqual(96, GrassLodBands.CellsPerSide(Window, 3));
        Assert.AreEqual(3, GrassLodBands.CellsPerSide(Window, 1000));
        Assert.AreEqual(0, GrassLodBands.CellsPerSide(0, 4));
    }

    [Test]
    public void FullDetailCapacityIsThePlacedCapacity()
    {
        int perCell = 10;
        int placed = Window * Window * perCell;
        Assert.AreEqual(placed, GrassLodBands.Capacity(Window, 1, perCell, placed));
    }

    [Test]
    public void CapacityHoldsEveryBladeTheBlockCanPlace()
    {
        int perCell = 10;
        int placed = Window * Window * perCell;
        for (int step = 1; step <= 128; step *= 2)
        {
            long side = Window / step;
            Assert.AreEqual((int)(side * side * perCell), GrassLodBands.Capacity(Window, step, perCell, placed), $"step {step}");
        }
    }

    [Test]
    public void CapacityShrinksWithTheSquareOfTheStep()
    {
        int placed = int.MaxValue;
        int one = GrassLodBands.Capacity(Window, 1, 6, placed);
        Assert.AreEqual(one / 4, GrassLodBands.Capacity(Window, 2, 6, placed));
        Assert.AreEqual(one / 16, GrassLodBands.Capacity(Window, 4, 6, placed));
        Assert.AreEqual(one / 64, GrassLodBands.Capacity(Window, 8, 6, placed));
    }

    [Test]
    public void CapacityIsNeverAbovePlacedCapacity()
    {
        for (int step = 1; step <= 128; step *= 2)
            Assert.LessOrEqual(GrassLodBands.Capacity(Window, step, 40, 5000), 5000);
    }

    [Test]
    public void InvalidStepGivesTheCapacityOfItsValidStep()
    {
        Assert.AreEqual(GrassLodBands.Capacity(Window, 4, 10, int.MaxValue), GrassLodBands.Capacity(Window, 3, 10, int.MaxValue));
        Assert.AreEqual(GrassLodBands.Capacity(Window, 1, 10, int.MaxValue), GrassLodBands.Capacity(Window, -2, 10, int.MaxValue));
    }

    [Test]
    public void InvalidInputHasNoCapacity()
    {
        Assert.AreEqual(0, GrassLodBands.Capacity(0, 4, 10, 1000));
        Assert.AreEqual(0, GrassLodBands.Capacity(Window, 4, 0, 1000));
        Assert.AreEqual(0, GrassLodBands.Capacity(Window, 4, 10, 0));
    }

    [Test]
    public void RingPoolHoldsEveryWedgeBlockOfThatRing()
    {
        Assert.AreEqual(0, ViewWedge.MaxBlocksInRing(0));
        Assert.AreEqual(0, ViewWedge.MaxBlocksInRing(ViewWedge.MaxBlockRadius + 1));

        for (int ring = 1; ring <= GrassLodBands.FarRings; ring++)
        {
            int pool = ViewWedge.MaxBlocksInRing(ring);
            Assert.Greater(pool, 0, $"ring {ring}");
            Assert.LessOrEqual(pool, 8 * ring, $"ring {ring}");

            for (int s = 0; s < ViewSector.Count; s++)
            {
                ViewSector sector = ViewSector.FromIndex(s);
                int n = 0;
                for (int bz = -ring; bz <= ring; bz++)
                for (int bx = -ring; bx <= ring; bx++)
                    if (ViewWedge.Ring(bx, bz) == ring && ViewWedge.Contains(sector, bx, bz)) n++;
                Assert.LessOrEqual(n, pool, $"ring {ring} sector {s}");
            }
        }
    }

    [Test]
    public void RingPoolsCoverTheWedgeCollection()
    {
        var output = new ViewBlock[ViewWedge.MaxBlocks(GrassLodBands.FarRings)];
        for (int s = 0; s < ViewSector.Count; s++)
        {
            int count = ViewWedge.Collect(ViewSector.FromIndex(s), GrassLodBands.FarRings, output);
            int[] perRing = new int[GrassLodBands.FarRings + 1];
            for (int i = 0; i < count; i++) perRing[output[i].Ring]++;

            for (int ring = 1; ring <= GrassLodBands.FarRings; ring++)
                Assert.LessOrEqual(perRing[ring], ViewWedge.MaxBlocksInRing(ring), $"ring {ring} sector {s}");
        }
    }
}
