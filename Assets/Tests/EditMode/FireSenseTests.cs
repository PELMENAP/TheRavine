using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using TheRavine.Extensions;
using TheRavine.Generator;

public class FireSenseTests
{
    private static void Fill(int2[] cells, out NativeHashMap<long, FireCell> map, out NativeList<long> dense)
    {
        map   = new NativeHashMap<long, FireCell>(math.max(cells.Length, 16), Allocator.Persistent);
        dense = new NativeList<long>(math.max(cells.Length, 16), Allocator.Persistent);
        for (int i = 0; i < cells.Length; i++)
        {
            long key = Position2Int.Pack(cells[i].x, cells[i].y);
            if (!map.TryAdd(key, new FireCell { Left = 1f, Total = 1f, Size = 1f, Slot = dense.Length })) continue;
            dense.Add(key);
        }
    }

    private static void AssertSame(NativeHashMap<long, FireCell> map, NativeList<long> dense, float2 p, int radius, string what)
    {
        bool a = FireEvent.FindNearestBurning(map.AsReadOnly(), p, radius, out float2 ca, out float da);
        bool b = FireEvent.FindNearestBurningDense(dense.AsArray(), p, radius, out float2 cb, out float db);
        Assert.AreEqual(a, b, what);
        Assert.AreEqual(da, db, what);
        Assert.AreEqual(ca.x, cb.x, what);
        Assert.AreEqual(ca.y, cb.y, what);
    }

    [Test]
    public void DenseScanMatchesTheSquareScanOnRandomInputs()
    {
        var rng = new System.Random(5);
        for (int round = 0; round < 400; round++)
        {
            int n = rng.Next(0, 90);
            var cells = new int2[n];
            for (int i = 0; i < n; i++) cells[i] = new int2(rng.Next(-20, 21), rng.Next(-20, 21));
            Fill(cells, out var map, out var dense);
            try
            {
                float2 p = round % 3 == 0
                    ? FireEvent.CellCenter(Position2Int.Pack(rng.Next(-5, 6), rng.Next(-5, 6)))
                    : new float2((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 40f - 20f) * MapGenerator.scale;
                AssertSame(map, dense, p, rng.Next(0, 16), $"round {round}");
            }
            finally
            {
                map.Dispose();
                dense.Dispose();
            }
        }
    }

    [Test]
    public void SymmetricCellsAtTheSameDistanceResolveTheSameWay()
    {
        Fill(new[] { new int2(3, 0), new int2(-3, 0), new int2(0, 3), new int2(0, -3) }, out var map, out var dense);
        try
        {
            AssertSame(map, dense, FireEvent.CellCenter(Position2Int.Pack(0, 0)), 5, "four-way tie");
        }
        finally
        {
            map.Dispose();
            dense.Dispose();
        }
    }

    [Test]
    public void EmptySetFindsNothing()
    {
        Fill(System.Array.Empty<int2>(), out var map, out var dense);
        try
        {
            var p = FireEvent.CellCenter(Position2Int.Pack(0, 0));
            Assert.IsFalse(FireEvent.FindNearestBurningDense(dense.AsArray(), p, 8, out _, out float distance));
            Assert.AreEqual(-1f, distance);
            AssertSame(map, dense, p, 8, "empty");
        }
        finally
        {
            map.Dispose();
            dense.Dispose();
        }
    }

    [Test]
    public void OneCellOnTheRadiusIsFoundAndOneBeyondIsNot()
    {
        var p = FireEvent.CellCenter(Position2Int.Pack(0, 0));
        foreach (int cell in new[] { 6, 7 })
        {
            Fill(new[] { new int2(cell, 0) }, out var map, out var dense);
            try
            {
                bool found = FireEvent.FindNearestBurningDense(dense.AsArray(), p, 6, out _, out float distance);
                Assert.AreEqual(cell == 6, found, $"cell {cell}");
                if (found) Assert.AreEqual(6f * MapGenerator.scale, distance, 1e-4f);
                AssertSame(map, dense, p, 6, $"cell {cell}");
            }
            finally
            {
                map.Dispose();
                dense.Dispose();
            }
        }
    }

    [Test]
    public void RadiusZeroAndNegativeFindNothing()
    {
        Fill(new[] { new int2(0, 0) }, out var map, out var dense);
        try
        {
            var p = FireEvent.CellCenter(Position2Int.Pack(0, 0));
            Assert.IsFalse(FireEvent.FindNearestBurningDense(dense.AsArray(), p, 0, out _, out _));
            Assert.IsFalse(FireEvent.FindNearestBurningDense(dense.AsArray(), p, -3, out _, out _));
        }
        finally
        {
            map.Dispose();
            dense.Dispose();
        }
    }

    [Test]
    public void DenseScanDoesNotAllocate()
    {
        Fill(new[] { new int2(3, 0), new int2(0, 5), new int2(-4, 2) }, out var map, out var dense);
        try
        {
            var cells = dense.AsArray();
            var p = FireEvent.CellCenter(Position2Int.Pack(0, 0));
            float sink = 0f;
            AllocationAssert.Zero(() =>
            {
                FireEvent.FindNearestBurningDense(cells, p, 12, out float2 c, out float d);
                sink += c.x + d;
            }, 200, "FireEvent.FindNearestBurningDense");
            Assert.Greater(sink, 0f);
        }
        finally
        {
            map.Dispose();
            dense.Dispose();
        }
    }
}
