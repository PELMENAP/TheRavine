using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public class WorldCommandBufferTests
{
    private sealed class FakeWorld : IWorldCommandTarget
    {
        public readonly Dictionary<long, int> Objects = new();
        public readonly List<string> Log = new();

        public bool TryGetObject(long worldPos, out ObjectInstInfo info)
        {
            if (Objects.TryGetValue(worldPos, out int id))
            {
                info = new ObjectInstInfo(Vector3.zero, id, 1);
                return true;
            }

            info = default;
            return false;
        }

        public bool RemoveObject(long worldPos)
        {
            Log.Add($"remove {worldPos}");
            return Objects.Remove(worldPos);
        }

        public bool TryAddObject(long worldPos, Vector3 realPos, int prefabID, int amount, ReadOnlySpan<Vector2Int> additionalWorldCells, int levels)
        {
            Log.Add($"add {worldPos} {prefabID}");
            if (Objects.ContainsKey(worldPos)) return false;

            Objects[worldPos] = prefabID;
            return true;
        }

        public Vector3 GetRealPosition(long worldPos) => Vector3.zero;
    }

    private WorldCommandBuffer _buffer;
    private FakeWorld _world;
    private WorldCommandStats _stats;

    [SetUp]
    public void SetUp()
    {
        _buffer = new WorldCommandBuffer(4);
        _world = new FakeWorld();
        _stats = new WorldCommandStats();
    }

    [TearDown]
    public void TearDown() => _buffer.Dispose();

    private static WorldCommand Add(long pos, int prefab) => new WorldCommand
    {
        Kind = WorldCommand.Add,
        Pos = pos,
        PrefabId = prefab,
        Amount = 1,
        ExpectedPrefabId = WorldCommand.NoExpectation,
        Levels = 1
    };

    private static WorldCommand Remove(long pos, int expected = WorldCommand.NoExpectation) => new WorldCommand
    {
        Kind = WorldCommand.Remove,
        Pos = pos,
        ExpectedPrefabId = expected
    };

    [Test]
    public void CommandsApplyInPushOrder()
    {
        _buffer.Push(Add(1, 5));
        _buffer.Push(Remove(1));
        _buffer.Push(Add(1, 7));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        CollectionAssert.AreEqual(new[] { "add 1 5", "remove 1", "add 1 7" }, _world.Log);
        Assert.AreEqual(7, _world.Objects[1]);
        Assert.AreEqual(0, _buffer.Count);
        Assert.AreEqual(2, _stats.AddApplied);
        Assert.AreEqual(1, _stats.RemoveApplied);
    }

    [Test]
    public void ExpectedPrefabMismatchIsStaleAndKeepsObject()
    {
        _world.Objects[1] = 5;
        _buffer.Push(Remove(1, expected: 9));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.AreEqual(1, _stats.RemoveStale);
        Assert.AreEqual(0, _stats.RemoveApplied);
        Assert.IsTrue(_world.Objects.ContainsKey(1));
        Assert.AreEqual(0, _world.Log.Count);
    }

    [Test]
    public void ExpectedPrefabOfMissingObjectIsStale()
    {
        _buffer.Push(Remove(1, expected: 5));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.AreEqual(1, _stats.RemoveStale);
    }

    [Test]
    public void MatchingExpectationRemoves()
    {
        _world.Objects[1] = 5;
        _buffer.Push(Remove(1, expected: 5));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.AreEqual(1, _stats.RemoveApplied);
        Assert.IsFalse(_world.Objects.ContainsKey(1));
    }

    [Test]
    public void UncheckedRemoveOfMissingObjectHasNoEffect()
    {
        _buffer.Push(Remove(1));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.AreEqual(1, _stats.RemoveNoEffect);
        Assert.AreEqual(0, _stats.RemoveStale);
    }

    [Test]
    public void AddOnOccupiedCellIsRejected()
    {
        _world.Objects[1] = 5;
        _buffer.Push(Add(1, 7));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.AreEqual(1, _stats.AddRejected);
        Assert.AreEqual(5, _world.Objects[1]);
    }

    [Test]
    public void ExhaustedBudgetAppliesOneCommandAndCarriesRemainder()
    {
        for (int i = 0; i < 5; i++) _buffer.Push(Add(i, 1));

        int processed = _buffer.Apply(_world, 0L, _stats);

        Assert.AreEqual(1, processed);
        Assert.AreEqual(4, _buffer.Count);

        _buffer.Apply(_world, 0L, _stats);
        Assert.AreEqual(3, _buffer.Count);

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);
        Assert.AreEqual(0, _buffer.Count);
        Assert.AreEqual(5, _world.Objects.Count);
    }

    [Test]
    public void CarriedCommandsKeepOrderAcrossFrames()
    {
        _buffer.Push(Add(1, 5));
        _buffer.Push(Remove(1));

        _buffer.Apply(_world, 0L, _stats);
        _buffer.Push(Add(1, 8));
        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        CollectionAssert.AreEqual(new[] { "add 1 5", "remove 1", "add 1 8" }, _world.Log);
    }

    [Test]
    public void AppliedAddsListsOnlySuccessfulAdds()
    {
        _world.Objects[2] = 5;
        _buffer.Push(Add(1, 7));
        _buffer.Push(Add(2, 7));
        _buffer.Push(Remove(2));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.AreEqual(1, _buffer.AppliedAdds.Length);
        Assert.AreEqual(1L, _buffer.AppliedAdds[0].Pos);
    }

    [Test]
    public void ApplyOneReportsResultWithoutBuffering()
    {
        WorldCommand add = Add(1, 5);

        Assert.IsTrue(WorldCommandBuffer.ApplyOne(_world, in add, _stats));
        Assert.IsFalse(WorldCommandBuffer.ApplyOne(_world, in add, _stats));
        Assert.AreEqual(0, _buffer.Count);
        Assert.AreEqual(1, _stats.AddApplied);
        Assert.AreEqual(1, _stats.AddRejected);
    }

    [Test]
    public void PendingRemoveCoversWholeCellUntilApplied()
    {
        long pos = Position2Int.Pack(10, 10);
        long sameCell = Position2Int.Pack(11, 11);
        long otherCell = Position2Int.Pack(12, 10);
        _buffer.Push(Remove(pos));

        Assert.IsTrue(_buffer.HasPendingRemove(pos));
        Assert.IsTrue(_buffer.HasPendingRemove(sameCell));
        Assert.IsFalse(_buffer.HasPendingRemove(otherCell));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.IsFalse(_buffer.HasPendingRemove(pos));
    }

    [Test]
    public void PendingRemovesAreCountedPerCell()
    {
        long pos = Position2Int.Pack(10, 10);
        _buffer.Push(Remove(pos));
        _buffer.Push(Remove(Position2Int.Pack(11, 10)));

        _buffer.Apply(_world, 0L, _stats);

        Assert.IsTrue(_buffer.HasPendingRemove(pos));

        _buffer.Apply(_world, FrameBudget.Unlimited, _stats);

        Assert.IsFalse(_buffer.HasPendingRemove(pos));
    }

    [Test]
    public void AddsDoNotMarkPendingRemoves()
    {
        long pos = Position2Int.Pack(10, 10);
        _buffer.Push(Add(pos, 3));

        Assert.IsFalse(_buffer.HasPendingRemove(pos));
    }

    [Test]
    public void DisposeReleasesAllocationsAndIsRepeatable()
    {
        var local = new WorldCommandBuffer(4);
        local.Push(Add(1, 1));

        local.Dispose();

        Assert.IsFalse(local.IsCreated);
        local.Dispose();
    }
}
