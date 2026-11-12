using NUnit.Framework;
using UnityEngine;
using TheRavine.Generator;

public class GrassLodBandsTests
{
    private const float Tolerance = 1e-5f;

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, Tolerance);
        Assert.AreEqual(expected.y, actual.y, Tolerance);
        Assert.AreEqual(expected.z, actual.z, Tolerance);
    }

    [Test]
    public void SortedDistancesArePacked()
    {
        Vector4 packed = GrassLodBands.PackEnds(new Vector3(40f, 90f, 160f), 12f);
        Assert.AreEqual(new Vector4(40f, 90f, 160f, 12f), packed);
    }

    [Test]
    public void UnsortedDistancesAreRaisedToThePreviousEnd()
    {
        AssertVector(new Vector3(100f, 100f, 100f), GrassLodBands.ClampEnds(new Vector3(100f, 40f, 70f)));
        AssertVector(new Vector3(30f, 30f, 80f), GrassLodBands.ClampEnds(new Vector3(30f, 10f, 80f)));
    }

    [Test]
    public void NegativeAndNonFiniteDistancesCollapseToZero()
    {
        AssertVector(new Vector3(0f, 0f, 50f), GrassLodBands.ClampEnds(new Vector3(-5f, float.NaN, 50f)));
        Vector3 huge = GrassLodBands.ClampEnds(new Vector3(10f, float.PositiveInfinity, float.PositiveInfinity));
        Assert.AreEqual(GrassLodBands.MaxDistance, huge.y);
        Assert.AreEqual(GrassLodBands.MaxDistance, huge.z);
    }

    [Test]
    public void NegativeFadeWidthIsZero()
    {
        Assert.AreEqual(0f, GrassLodBands.PackEnds(new Vector3(10f, 20f, 30f), -3f).w);
        Assert.AreEqual(0f, GrassLodBands.PackEnds(new Vector3(10f, 20f, 30f), float.NaN).w);
    }

    [Test]
    public void KeepFractionsAreClampedToUnitRange()
    {
        AssertVector(new Vector3(1f, 1f, 0f), GrassLodBands.ClampKeep(new Vector3(2f, 3f, -1f)));
        AssertVector(new Vector3(0f, 0f, 0f), GrassLodBands.ClampKeep(new Vector3(float.NaN, 0.5f, 0.2f)));
    }

    [Test]
    public void KeepFractionsDoNotIncreaseWithDistance()
    {
        AssertVector(new Vector3(0.5f, 0.5f, 0.2f), GrassLodBands.ClampKeep(new Vector3(0.5f, 0.9f, 0.2f)));
        AssertVector(new Vector3(1f, 0.25f, 0.25f), GrassLodBands.ClampKeep(new Vector3(1f, 0.25f, 0.6f)));
    }

    [Test]
    public void PackedKeepHasZeroAsTheNextKeepOfTheLastBand()
    {
        Vector4 packed = GrassLodBands.PackKeep(new Vector3(1f, 0.5f, 0.25f));
        Assert.AreEqual(new Vector4(1f, 0.5f, 0.25f, 0f), packed);
    }

    [Test]
    public void CapacityIsNeverAbovePlacedCapacity()
    {
        Vector3 ends = new(60f, 120f, 200f);
        int perCell = 10;
        int placed = 5000;
        for (int band = 0; band < GrassLodBands.BandCount; band++)
            Assert.LessOrEqual(GrassLodBands.Capacity(band, ends, 384, perCell, placed), placed);
    }

    [Test]
    public void CapacityIsNeverAboveTheWindowArea()
    {
        int side = 64;
        int perCell = 3;
        int placed = side * side * perCell;
        Vector3 ends = new(1000f, 2000f, 3000f);
        Assert.AreEqual(placed, GrassLodBands.Capacity(0, ends, side, perCell, placed));
        Assert.AreEqual(placed, GrassLodBands.Capacity(1, ends, side, perCell, placed));
        Assert.AreEqual(placed, GrassLodBands.Capacity(2, ends, side, perCell, placed));
    }

    [Test]
    public void CapacityHoldsAFullDensityBandForAnyPivot()
    {
        int side = 48;
        int perCell = 4;
        int placed = side * side * perCell;
        Vector3 ends = new(9f, 17f, 30f);
        float[] pivots = { -20f, 0f, 5.5f, 24f, 47f, 70f };

        for (int band = 0; band < GrassLodBands.BandCount; band++)
        {
            int capacity = GrassLodBands.Capacity(band, ends, side, perCell, placed);
            float lo = band == 0 ? 0f : ends[band - 1];
            float hi = ends[band];

            for (int px = 0; px < pivots.Length; px++)
            {
                for (int pz = 0; pz < pivots.Length; pz++)
                {
                    int cells = 0;
                    for (int z = 0; z < side; z++)
                    {
                        for (int x = 0; x < side; x++)
                        {
                            if (CellTouchesBand(x, z, pivots[px], pivots[pz], lo, hi)) cells++;
                        }
                    }

                    Assert.GreaterOrEqual(capacity, cells * perCell, $"band {band} pivot ({pivots[px]}, {pivots[pz]})");
                }
            }
        }
    }

    [Test]
    public void CapacityScalesWithBladesPerCell()
    {
        Vector3 ends = new(30f, 60f, 90f);
        int single = GrassLodBands.Capacity(1, ends, 384, 1, int.MaxValue);
        int tenfold = GrassLodBands.Capacity(1, ends, 384, 10, int.MaxValue);
        Assert.Greater(single, 0);
        Assert.AreEqual(single * 10, tenfold);
    }

    [Test]
    public void EmptyBandAndInvalidInputHaveNoCapacity()
    {
        Vector3 ends = new(50f, 50f, 100f);
        Assert.AreEqual(0, GrassLodBands.Capacity(1, ends, 384, 10, 100000));
        Assert.AreEqual(0, GrassLodBands.Capacity(-1, ends, 384, 10, 100000));
        Assert.AreEqual(0, GrassLodBands.Capacity(GrassLodBands.BandCount, ends, 384, 10, 100000));
        Assert.AreEqual(0, GrassLodBands.Capacity(0, ends, 0, 10, 100000));
        Assert.AreEqual(0, GrassLodBands.Capacity(0, ends, 384, 0, 100000));
        Assert.AreEqual(0, GrassLodBands.Capacity(0, ends, 384, 10, 0));
    }

    [Test]
    public void UnsortedEndsGiveTheSameCapacityAsTheirClamp()
    {
        Vector3 raw = new(80f, 30f, 120f);
        Vector3 clamped = GrassLodBands.ClampEnds(raw);
        for (int band = 0; band < GrassLodBands.BandCount; band++)
            Assert.AreEqual(
                GrassLodBands.Capacity(band, clamped, 384, 5, int.MaxValue),
                GrassLodBands.Capacity(band, raw, 384, 5, int.MaxValue));
    }

    private static bool CellTouchesBand(int cellX, int cellZ, float pivotX, float pivotZ, float lo, float hi)
    {
        for (int sz = 0; sz <= 2; sz++)
        {
            for (int sx = 0; sx <= 2; sx++)
            {
                float x = cellX - 0.5f + sx * 0.5f;
                float z = cellZ - 0.5f + sz * 0.5f;
                float dx = x - pivotX;
                float dz = z - pivotZ;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d >= lo && d < hi) return true;
            }
        }
        return false;
    }
}
