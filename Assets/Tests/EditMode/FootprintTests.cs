using System;
using NUnit.Framework;
using UnityEngine;
using TheRavine.Generator;

public class FootprintTests
{
    private static readonly Vector2Int[] Offsets = { new(1, 0), new(0, 1), new(-1, -1) };
    private static readonly Vector2Int[] Buffer = new Vector2Int[ObjectInstInfo.MaxSecondary];

    [Test]
    public void ResolveReturnsOffsetCountAndWorldCellsScaledFromOrigin()
    {
        var origin = new Vector2Int(10, 20);
        Span<Vector2Int> cells = stackalloc Vector2Int[ObjectInstInfo.MaxSecondary];

        int count = Footprint.Resolve(Offsets, origin, cells);

        Assert.AreEqual(3, count);
        Assert.AreEqual(new Vector2Int(10 + MapGenerator.scale, 20), cells[0]);
        Assert.AreEqual(new Vector2Int(10, 20 + MapGenerator.scale), cells[1]);
        Assert.AreEqual(new Vector2Int(10 - MapGenerator.scale, 20 - MapGenerator.scale), cells[2]);
    }

    [Test]
    public void ResolveReportsOverflowWithoutWriting()
    {
        Span<Vector2Int> cells = stackalloc Vector2Int[2];

        int count = Footprint.Resolve(Offsets, Vector2Int.zero, cells);

        Assert.AreEqual(3, count);
        Assert.AreEqual(Vector2Int.zero, cells[0]);
        Assert.AreEqual(Vector2Int.zero, cells[1]);
    }

    [Test]
    public void ObjectWithoutFootprintResolvesToNoCells()
    {
        var info = ScriptableObject.CreateInstance<ObjectInfo>();
        try
        {
            Span<Vector2Int> cells = stackalloc Vector2Int[ObjectInstInfo.MaxSecondary];
            Assert.AreEqual(0, Footprint.Resolve(info, new Vector2Int(4, 4), cells));
            Assert.AreEqual(0, Footprint.Resolve((ObjectInfo)null, new Vector2Int(4, 4), cells));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(info);
        }
    }

    [Test]
    public void ResolveDoesNotAllocate()
    {
        AllocationAssert.Zero(() => Footprint.Resolve(Offsets, new Vector2Int(3, 7), Buffer), 1000, "Footprint.Resolve");
    }
}
