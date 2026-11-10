using System;
using System.IO;
using NUnit.Framework;
using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;
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

    private string FileOf(long key) =>
        Path.Combine(ChunkStore.DirectoryFor(_worldId), $"{Position2Int.GetX(key)}_{Position2Int.GetY(key)}.chunk");

    private static int IndexOf(byte[] data, float value)
    {
        byte[] pattern = BitConverter.GetBytes(value);
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int k = 0; k < pattern.Length && match; k++)
                match = data[i + k] == pattern[k];
            if (match) return i;
        }
        return -1;
    }

    private void AssertRestoreRefused(long key, byte[] data, string what)
    {
        File.WriteAllBytes(FileOf(key), data);
        Assert.IsFalse(_store.TryRestore(key, _restored), what);
        Assert.AreEqual(0, _restored.Objects.Length, what);
        Assert.AreEqual(0, _restored.HeightEdits.Count(), what);
    }

    [Test]
    public void NonFiniteObjectPositionIsRejected()
    {
        long key = Position2Int.Pack(1, 1);
        Assert.IsTrue(_source.TryAddObject(10, new ObjectInstInfo(new Vector3(777f, 888f, -999f), 5, 1)));
        Assert.IsTrue(_store.TrySave(key, _source));
        byte[] good = File.ReadAllBytes(FileOf(key));

        foreach (float coord in new[] { 777f, 888f, -999f })
        {
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                byte[] data = (byte[])good.Clone();
                int at = IndexOf(data, coord);
                Assert.GreaterOrEqual(at, 0);
                BitConverter.GetBytes(bad).CopyTo(data, at);
                AssertRestoreRefused(key, data, $"{coord} -> {bad}");
            }
        }

        File.WriteAllBytes(FileOf(key), good);
        Assert.IsTrue(_store.TryRestore(key, _restored));
        Assert.AreEqual(1, _restored.Objects.Length);
    }

    [Test]
    public void NonFiniteTerrainEditHeightIsRejected()
    {
        long key = Position2Int.Pack(2, 1);
        Assert.IsTrue(_source.TryAddObject(10, Info(1)));
        _source.HeightEdits[20] = 12345.5f;
        Assert.IsTrue(_store.TrySave(key, _source));
        byte[] good = File.ReadAllBytes(FileOf(key));

        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            byte[] data = (byte[])good.Clone();
            int at = IndexOf(data, 12345.5f);
            Assert.GreaterOrEqual(at, 0);
            BitConverter.GetBytes(bad).CopyTo(data, at);
            AssertRestoreRefused(key, data, $"height -> {bad}");
        }

        File.WriteAllBytes(FileOf(key), good);
        Assert.IsTrue(_store.TryRestore(key, _restored));
        Assert.AreEqual(12345.5f, _restored.HeightEdits[20]);
    }

    [Test]
    public void ObjectsMissingFromTheCatalogAreSkipped()
    {
        long key = Position2Int.Pack(3, 1);
        Assert.IsTrue(_source.TryAddObject(10, Info(1)));
        Assert.IsTrue(_source.TryAddObject(20, Info(2)));
        Assert.IsTrue(_source.TryAddObject(30, Info(60000)));
        Assert.IsTrue(_store.TrySave(key, _source));

        var info = ScriptableObject.CreateInstance<ObjectInfo>();
        var catalog = ScriptableObject.CreateInstance<ObjectInfoRegistry>();
        try
        {
            info.EditorAssignId(1);
            catalog.objectInfos.Add(info);
            catalog.Rebuild();

            Assert.IsTrue(_store.TryRestore(key, _restored, 0L, catalog));

            Assert.AreEqual(1, _restored.Objects.Length);
            Assert.AreEqual(1, _restored.Objects[0].PrefabID);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(info);
        }
    }

    [Test]
    public void OversizedFileIsRejectedWithoutReading()
    {
        long key = Position2Int.Pack(4, 1);
        Assert.IsTrue(_source.TryAddObject(10, Info(1)));
        Assert.IsTrue(_store.TrySave(key, _source));
        byte[] good = File.ReadAllBytes(FileOf(key));

        var padded = new byte[64 + ChunkData.TotalCells * 72L + 1];
        Array.Copy(good, padded, good.Length);
        AssertRestoreRefused(key, padded, "padded");

        File.WriteAllBytes(FileOf(key), good);
        Assert.IsTrue(_store.TryRestore(key, _restored));
    }

    [Test]
    public void RandomTailsAfterAValidHeaderNeverThrow()
    {
        long key = Position2Int.Pack(5, 1);
        Assert.IsTrue(_source.TryAddObject(10, Info(1)));
        Assert.IsTrue(_store.TrySave(key, _source));
        byte[] good = File.ReadAllBytes(FileOf(key));
        const int headerBytes = 45;

        var rng = new System.Random(4321);
        for (int i = 0; i < 200; i++)
        {
            var data = new byte[headerBytes + rng.Next(0, 600)];
            Array.Copy(good, data, headerBytes);
            var tail = new byte[data.Length - headerBytes];
            rng.NextBytes(tail);
            tail.CopyTo(data, headerBytes);

            File.WriteAllBytes(FileOf(key), data);
            Assert.DoesNotThrow(() => _store.TryRestore(key, _restored), $"iteration {i}");
            _restored.ClearObjects();
        }
    }
}
