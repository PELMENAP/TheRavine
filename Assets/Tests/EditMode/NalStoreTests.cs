using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public class NalStoreTests
{
    private NalStore _store;

    [SetUp]
    public void SetUp() => _store = new NalStore(4);

    [TearDown]
    public void TearDown() => _store.Dispose();

    private static long Key(int x, int y) => Position2Int.Pack(x, y);

    [Test]
    public void AddStoresAllFields()
    {
        Assert.IsTrue(_store.Add(new Vector2Int(3, -7), 11, 2, 4.5d));

        Assert.AreEqual(1, _store.Count);
        Assert.IsTrue(_store.TryGetSlot(Key(3, -7), out int slot));
        Assert.AreEqual(new int2(3, -7), _store.Cell[slot]);
        Assert.AreEqual(11, _store.PrefabId[slot]);
        Assert.AreEqual(2, _store.Dense[slot]);
        Assert.AreEqual(4.5d, _store.Due[slot]);
    }

    [Test]
    public void AddRejectsDuplicatePosition()
    {
        Assert.IsTrue(_store.Add(new Vector2Int(1, 1), 1, 0, 0d));
        Assert.IsFalse(_store.Add(new Vector2Int(1, 1), 2, 1, 5d));

        Assert.AreEqual(1, _store.Count);
        Assert.AreEqual(1, _store.PrefabId[0]);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void RemoveMissingReturnsFalse()
    {
        _store.Add(new Vector2Int(1, 1), 1, 0, 0d);

        Assert.IsFalse(_store.Remove(Key(9, 9)));
        Assert.AreEqual(1, _store.Count);
    }

    [Test]
    public void RemoveMovesLastSlotIntoHole()
    {
        for (int i = 0; i < 5; i++) _store.Add(new Vector2Int(i, i * 2), 100 + i, i, i);

        Assert.IsTrue(_store.Remove(Key(1, 2)));

        Assert.AreEqual(4, _store.Count);
        Assert.IsFalse(_store.Contains(Key(1, 2)));
        Assert.IsTrue(_store.TryGetSlot(Key(4, 8), out int slot));
        Assert.AreEqual(1, slot);
        Assert.AreEqual(104, _store.PrefabId[slot]);
        Assert.AreEqual(4, _store.Dense[slot]);
        Assert.AreEqual(4d, _store.Due[slot]);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void RemoveLastSlotKeepsInvariant()
    {
        for (int i = 0; i < 3; i++) _store.Add(new Vector2Int(i, 0), i, i, i);

        Assert.IsTrue(_store.Remove(Key(2, 0)));

        Assert.AreEqual(2, _store.Count);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void InvariantHoldsAfterMixedAddRemove()
    {
        const int total = 200;
        for (int i = 0; i < total; i++) _store.Add(new Vector2Int(i, -i), i, i % 7, i);

        for (int i = 0; i < total; i += 3)
        {
            Assert.IsTrue(_store.Remove(Key(i, -i)));
            Assert.IsTrue(_store.IsConsistent());
        }

        for (int i = 0; i < total; i += 3) Assert.IsTrue(_store.Add(new Vector2Int(i, -i), 1000 + i, 0, 0d));

        Assert.AreEqual(total, _store.Count);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void ClearEmptiesAllColumns()
    {
        for (int i = 0; i < 10; i++) _store.Add(new Vector2Int(i, i), i, 0, 0d);

        _store.Clear();

        Assert.AreEqual(0, _store.Count);
        Assert.AreEqual(0, _store.SlotByPos.Count);
        Assert.IsFalse(_store.Contains(Key(1, 1)));
        Assert.IsTrue(_store.Add(new Vector2Int(1, 1), 1, 0, 0d));
    }

    [Test]
    public void DisposeReleasesAllocations()
    {
        var local = new NalStore(4);
        local.Add(new Vector2Int(0, 0), 1, 0, 0d);

        local.Dispose();

        Assert.IsFalse(local.IsCreated);
        Assert.IsFalse(local.SlotByPos.IsCreated);
        local.Dispose();
    }

    [Test]
    public void TierFollowsSlotThroughSwapBack()
    {
        _store.Add(new Vector2Int(0, 0), 1, 0, 0d, 0);
        _store.Add(new Vector2Int(1, 0), 2, 0, 0d, 1);
        _store.Add(new Vector2Int(2, 0), 3, 0, 0d, 2);

        _store.Remove(Key(0, 0));

        Assert.IsTrue(_store.TryGetSlot(Key(2, 0), out int slot));
        Assert.AreEqual(2, _store.Tier[slot]);
        Assert.IsTrue(_store.TryGetSlot(Key(1, 0), out slot));
        Assert.AreEqual(1, _store.Tier[slot]);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void ClearForgetsRegisteredChunks()
    {
        _store.Registered[7L] = new NalChunkState { Detail = 1, Ring = 2, Stamp = 3 };

        _store.Clear();

        Assert.AreEqual(0, _store.Registered.Count);
    }
}
