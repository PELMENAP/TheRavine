using System;
using System.IO;
using NUnit.Framework;
using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.Generator;
using UnityEngine;
using Unity.Collections;

public class ChunkStoreTests
{
    private const int Seed = 42;
    private string _worldId;
    private ChunkStore _store;
    private ChunkData _source;
    private ChunkData _restored;

    [SetUp]
    public void SetUp()
    {
        _worldId  = "chunk_store_test_" + Guid.NewGuid().ToString("N");
        _store    = ChunkStore.ForWorld(_worldId, Seed);
        _source   = new ChunkData();
        _restored = new ChunkData();
    }

    [TearDown]
    public void TearDown()
    {
        _source.Dispose();
        _restored.Dispose();
        Directory.Delete(WorldRegistry.GetWorldDirectory(_worldId), true);
    }

    private static ObjectInstInfo Info(int prefab) => new ObjectInstInfo(new Vector3(prefab, 0.5f, -prefab), prefab, prefab * 3);

    [Test]
    public void RestoredChunkKeepsObjectsOccupancyAndLevels()
    {
        long key = Position2Int.Pack(3, -2);
        Assert.IsTrue(_source.TryAddObject(10, Info(1)));
        Assert.IsTrue(_source.TryAddObject(200, Info(2), new[] { 201, 202 }));
        Assert.IsTrue(_source.TryAddObject(300, Info(3), default, 2));
        Assert.IsTrue(_source.TryAddObject(400, Info(4)));
        Assert.IsTrue(_source.RemoveObject(10));

        Assert.IsTrue(_store.TrySave(key, _source));
        Assert.IsTrue(_store.TryRestore(key, _restored));

        Assert.AreEqual(_source.Objects.Length, _restored.Objects.Length);
        for (int i = 0; i < _source.Objects.Length; i++)
        {
            ObjectInstInfo expected = _source.Objects[i];
            ObjectInstInfo actual = _restored.Objects[i];
            Assert.AreEqual(expected.PrefabID, actual.PrefabID);
            Assert.AreEqual(expected.Position, actual.Position);
            Assert.AreEqual(expected.Amount, actual.Amount);
            Assert.AreEqual(expected.PrimaryIdx, actual.PrimaryIdx);
            Assert.AreEqual(expected.SecondaryCount, actual.SecondaryCount);
        }

        Assert.AreEqual(2, _restored.LevelsAt(300));
        Assert.IsTrue(_restored.HasLevelAt(300, 1));
        Assert.IsTrue(_restored.HasLevelAt(201, 0));
        Assert.IsFalse(_restored.NeedsSave);
    }

    [Test]
    public void SnapshotFromAnotherSeedIsIgnored()
    {
        long key = Position2Int.Pack(0, 0);
        Assert.IsTrue(_source.TryAddObject(5, Info(1)));
        Assert.IsTrue(_store.TrySave(key, _source));

        var otherSeedStore = ChunkStore.ForWorld(_worldId, Seed + 1);

        Assert.IsFalse(otherSeedStore.TryRestore(key, _restored));
        Assert.AreEqual(0, _restored.Objects.Length);
    }
}
