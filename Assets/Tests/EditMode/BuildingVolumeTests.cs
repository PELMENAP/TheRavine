using NUnit.Framework;
using UnityEngine;
using TheRavine.Generator;

public class BuildingVolumeTests
{
    private ChunkData _chunk;

    [SetUp]
    public void SetUp() => _chunk = new ChunkData();

    [TearDown]
    public void TearDown() => _chunk.Dispose();

    private static ObjectInstInfo Info() => new ObjectInstInfo(Vector3.zero, 1, 1);

    [Test]
    public void TwoLevelBuildingOccupiesBothLevelsOfItsCell()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(), default, 2));

        Assert.IsTrue(_chunk.HasLevelAt(10, 0));
        Assert.IsTrue(_chunk.HasLevelAt(10, 1));
        Assert.IsFalse(_chunk.HasLevelAt(10, 2));
        Assert.IsFalse(_chunk.HasLevelAt(11, 1));
    }

    [Test]
    public void AdjacentBuildingsDoNotIntersect()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(), new[] { 11 }, 2));
        Assert.IsTrue(_chunk.TryAddObject(12, Info(), default, 2));

        Assert.IsFalse(_chunk.TryAddObject(11, Info(), default, 2));
        Assert.IsFalse(_chunk.TryAddObject(12, Info(), default, 1));
    }

    [Test]
    public void VolumeIsRegisteredOnlyForMultiLevelBuildingsAndListsEveryLevel()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(), new[] { 11 }, 2));
        Assert.IsTrue(_chunk.TryAddObject(30, Info(), default, 1));

        Assert.AreEqual(1, _chunk.BuildingCount);
        Assert.IsTrue(_chunk.TryGetBuilding(10, out BuildingVolume volume));
        Assert.AreEqual(2, volume.Levels);
        Assert.AreEqual(4, volume.Cells.Length);
        Assert.IsFalse(_chunk.TryGetBuilding(30, out _));
    }

    [Test]
    public void RemovingBuildingFreesItsLevelsAndRegistryEntry()
    {
        Assert.IsTrue(_chunk.TryAddObject(10, Info(), new[] { 11 }, 2));

        Assert.IsTrue(_chunk.RemoveObject(11));

        Assert.IsFalse(_chunk.HasLevelAt(10, 1));
        Assert.IsFalse(_chunk.HasLevelAt(11, 1));
        Assert.IsFalse(_chunk.TryGetBuilding(10, out _));
        Assert.AreEqual(0, _chunk.BuildingCount);
    }

    [Test]
    public void LevelsOutsideRangeAreRejected()
    {
        Assert.IsFalse(_chunk.TryAddObject(10, Info(), default, 0));
        Assert.IsFalse(_chunk.TryAddObject(10, Info(), default, ObjectInfo.MaxLevels + 1));
    }
}
