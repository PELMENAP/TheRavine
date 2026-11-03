using NUnit.Framework;
using UnityEngine;
using TheRavine.Generator;

public class ChunkPlacementTests
{
    private ChunkData _chunk;

    [SetUp]
    public void SetUp() => _chunk = new ChunkData();

    [TearDown]
    public void TearDown() => _chunk.Dispose();

    private static ObjectInstInfo Info(int prefab = 1) => new ObjectInstInfo(Vector3.zero, prefab, 1);

    [Test]
    public void SecondaryCellsAreOccupiedAndNeighboursStayFree()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(), new[] { 11, 74 }, 1, out PlacementResult result));
        Assert.AreEqual(PlacementResult.Ok, result);

        Assert.IsTrue(_chunk.HasLevelAt(11, 0));
        Assert.IsTrue(_chunk.HasLevelAt(74, 0));
        Assert.IsFalse(_chunk.HasLevelAt(12, 0));

        Assert.IsFalse(_chunk.TryAddObject(74, Info(), default, 1, out result));
        Assert.AreEqual(PlacementResult.Occupied, result);
        Assert.IsFalse(_chunk.TryAddObject(12, Info(), new[] { 11 }, 1, out result));
        Assert.AreEqual(PlacementResult.Occupied, result);
        Assert.IsTrue(_chunk.TryAddObject(12, Info(), default, 1, out result));
    }

    [Test]
    public void SecondaryCellResolvesToTheSameObject()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(7), new[] { 11 }));

        Assert.IsTrue(_chunk.TryGetObject(11, out ObjectInstInfo info));
        Assert.AreEqual(7, info.PrefabID);
        Assert.AreEqual(10, info.PrimaryIdx);
        Assert.AreEqual(1, info.SecondaryCount);
    }

    [Test]
    public void RemovingThroughSecondaryCellFreesEveryCell()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(), new[] { 11, 12 }, 2));

        Assert.IsTrue(_chunk.RemoveObject(12));

        Assert.AreEqual(0, _chunk.Objects.Length);
        for (int cell = 10; cell <= 12; cell++)
        {
            Assert.IsFalse(_chunk.HasLevelAt(cell, 0));
            Assert.IsFalse(_chunk.HasLevelAt(cell, 1));
            Assert.IsFalse(_chunk.TryGetObject(cell, out _));
        }
        Assert.IsTrue(_chunk.TryAddObject(11, Info(), new[] { 10, 12 }, 2));
    }

    [Test]
    public void RejectionReasonsAreReported()
    {
        Assert.IsFalse(_chunk.TryAddObject(-1, Info(), default, 1, out PlacementResult result));
        Assert.AreEqual(PlacementResult.OutOfBounds, result);

        Assert.IsFalse(_chunk.TryAddObject(10, Info(), new[] { ChunkData.TotalCells }, 1, out result));
        Assert.AreEqual(PlacementResult.OutOfBounds, result);

        Assert.IsFalse(_chunk.TryAddObject(10, Info(), new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 1, out result));
        Assert.AreEqual(PlacementResult.TooManyCells, result);

        Assert.IsFalse(_chunk.TryAddObject(10, Info(), default, 0, out result));
        Assert.AreEqual(PlacementResult.InvalidLevels, result);

        Assert.IsFalse(_chunk.TryAddObject(10, Info(), new[] { 10 }, 1, out result));
        Assert.AreEqual(PlacementResult.Occupied, result);

        Assert.IsFalse(_chunk.TryAddObject(10, Info(), new[] { 11, 11 }, 1, out result));
        Assert.AreEqual(PlacementResult.Occupied, result);

        Assert.AreEqual(0, _chunk.Objects.Length);
    }

    [Test]
    public void RepeatedConstructionAtTheSameCellIsOccupied()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(), new[] { 11 }, 3, out PlacementResult result));

        Assert.IsFalse(_chunk.TryAddObject(10, Info(), new[] { 11 }, 3, out result));
        Assert.AreEqual(PlacementResult.Occupied, result);
    }

    [Test]
    public void UpperLevelIntersectionIsRejected()
    {
        _chunk.LevelMask[30] = 1 << 2;

        Assert.IsFalse(_chunk.TryAddObject(20, Info(), new[] { 30 }, 3, out PlacementResult result));
        Assert.AreEqual(PlacementResult.UpperLevelOccupied, result);

        Assert.IsTrue(_chunk.TryAddObject(20, Info(), new[] { 30 }, 2, out result));
        Assert.AreEqual(PlacementResult.Ok, result);
    }
}
