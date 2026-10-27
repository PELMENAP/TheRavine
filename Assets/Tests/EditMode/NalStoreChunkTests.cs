using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public class NalStoreChunkTests
{
    private NalStore _store;

    private static readonly Vector2Int[] ChunkA = { new(1, 1), new(2, 2), new(3, 3) };
    private static readonly Vector2Int[] ChunkB = { new(200, 5), new(201, 6) };

    [SetUp]
    public void SetUp() => _store = new NalStore(4);

    [TearDown]
    public void TearDown() => _store.Dispose();

    private static long KeyOf(Vector2Int p) => Position2Int.Pack(p);
    private static long ChunkOf(Vector2Int p) => MapGenerator.WorldToChunk(KeyOf(p));

    private void AddAll(params Vector2Int[] cells)
    {
        for (int i = 0; i < cells.Length; i++) _store.Add(cells[i], i + 1, i, i);
    }

    [Test]
    public void ChunkColumnMatchesWorldToChunk()
    {
        AddAll(ChunkA[0], ChunkB[0]);

        Assert.AreNotEqual(ChunkOf(ChunkA[0]), ChunkOf(ChunkB[0]));
        Assert.IsTrue(_store.TryGetSlot(KeyOf(ChunkB[0]), out int slot));
        Assert.AreEqual(ChunkOf(ChunkB[0]), _store.Chunk[slot]);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void RemoveChunkDropsAllItsSlotsAndKeepsOthers()
    {
        AddAll(ChunkA[0], ChunkB[0], ChunkA[1], ChunkB[1], ChunkA[2]);

        int removed = _store.RemoveChunk(ChunkOf(ChunkA[0]));

        Assert.AreEqual(3, removed);
        Assert.AreEqual(2, _store.Count);
        for (int i = 0; i < ChunkA.Length; i++) Assert.IsFalse(_store.Contains(KeyOf(ChunkA[i])));
        for (int i = 0; i < ChunkB.Length; i++) Assert.IsTrue(_store.Contains(KeyOf(ChunkB[i])));
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void RemoveChunkFromMiddleKeepsIndexInvariant()
    {
        AddAll(ChunkB[0], ChunkA[0], ChunkA[1], ChunkB[1]);

        _store.RemoveChunk(ChunkOf(ChunkA[0]));

        Assert.AreEqual(2, _store.Count);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void RemoveChunkAtTheEndKeepsIndexInvariant()
    {
        AddAll(ChunkA[0], ChunkA[1], ChunkB[0], ChunkB[1]);

        _store.RemoveChunk(ChunkOf(ChunkB[0]));

        Assert.AreEqual(2, _store.Count);
        Assert.IsTrue(_store.Contains(KeyOf(ChunkA[0])));
        Assert.IsTrue(_store.Contains(KeyOf(ChunkA[1])));
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void RemoveChunkOfEverythingEmptiesStore()
    {
        AddAll(ChunkA);

        Assert.AreEqual(3, _store.RemoveChunk(ChunkOf(ChunkA[0])));

        Assert.AreEqual(0, _store.Count);
        Assert.AreEqual(0, _store.SlotByPos.Count);
        Assert.IsTrue(_store.IsConsistent());
    }

    [Test]
    public void RemoveMissingChunkReturnsZero()
    {
        AddAll(ChunkA);

        Assert.AreEqual(0, _store.RemoveChunk(ChunkOf(ChunkB[0])));
        Assert.AreEqual(3, _store.Count);
    }

    [Test]
    public void LeftAtIsTakenOnce()
    {
        long chunk = ChunkOf(ChunkA[0]);
        _store.SetLeftAt(chunk, 12.5d);

        Assert.IsTrue(_store.TakeLeftAt(chunk, out double time));
        Assert.AreEqual(12.5d, time);
        Assert.IsFalse(_store.TakeLeftAt(chunk, out _));
    }

    [Test]
    public void ClearForgetsLeftAt()
    {
        long chunk = ChunkOf(ChunkA[0]);
        _store.SetLeftAt(chunk, 1d);

        _store.Clear();

        Assert.IsFalse(_store.TakeLeftAt(chunk, out _));
    }
}
