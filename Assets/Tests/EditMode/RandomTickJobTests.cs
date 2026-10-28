using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using TheRavine.Generator;

public class RandomTickJobTests
{
    private const int Prefab = 5;

    private NativeArray<int> _occupancy;
    private NativeList<ObjectInstInfo> _objects;
    private NativeArray<byte> _accept;
    private NativeArray<ChunkTickSource> _sources;
    private NativeArray<RandomTickEvent> _output;
    private NativeArray<int> _counter;

    [TearDown]
    public void TearDown()
    {
        if (_occupancy.IsCreated) _occupancy.Dispose();
        if (_objects.IsCreated) _objects.Dispose();
        if (_accept.IsCreated) _accept.Dispose();
        if (_sources.IsCreated) _sources.Dispose();
        if (_output.IsCreated) _output.Dispose();
        if (_counter.IsCreated) _counter.Dispose();
    }

    private void Setup(int handle, bool accepted, int outputCapacity, int chunks, int ring, int rounds)
    {
        _occupancy = new NativeArray<int>(ChunkData.TotalCells, Allocator.Persistent);
        for (int i = 0; i < _occupancy.Length; i++) _occupancy[i] = handle;

        _objects = new NativeList<ObjectInstInfo>(1, Allocator.Persistent);
        _objects.Add(new ObjectInstInfo(new Vector3(1f, 2f, 3f), Prefab, 1));

        _accept = new NativeArray<byte>(8, Allocator.Persistent);
        _accept[Prefab] = (byte)(accepted ? 1 : 0);

        _sources = new NativeArray<ChunkTickSource>(chunks, Allocator.Persistent);
        for (int i = 0; i < chunks; i++)
            _sources[i] = ChunkTickSource.Create(_occupancy, _objects, i, ring, rounds);

        _output = new NativeArray<RandomTickEvent>(outputCapacity, Allocator.Persistent);
        _counter = new NativeArray<int>(1, Allocator.Persistent);
    }

    private int Run(float cellsPerChunkPerTick, int chunks)
    {
        new RandomTickJob
        {
            Sources = _sources,
            AcceptById = _accept,
            Output = _output,
            Counter = _counter,
            CellsPerChunkPerTick = cellsPerChunkPerTick,
            FarCellDivisor = 8f,
            TotalCells = ChunkData.TotalCells,
            Seed = 12345u
        }.Run(chunks);

        return _counter[0];
    }

    [Test]
    public void WindowChunkSamplesConfiguredCells()
    {
        Setup(1, true, 1024, 3, 0, 1);
        Assert.AreEqual(48, Run(16f, 3));
    }

    [Test]
    public void FarRingSamplesDividedCells()
    {
        Setup(1, true, 1024, 3, 1, 1);
        Assert.AreEqual(6, Run(16f, 3));
    }

    [Test]
    public void CatchUpRoundsMultiplyCells()
    {
        Setup(1, true, 1024, 1, 0, 3);
        Assert.AreEqual(48, Run(16f, 1));
    }

    [Test]
    public void SecondaryAndEmptyCellsProduceNothing()
    {
        Setup(-1, true, 1024, 2, 0, 1);
        Assert.AreEqual(0, Run(16f, 2));
        TearDown();

        Setup(0, true, 1024, 2, 0, 1);
        Assert.AreEqual(0, Run(16f, 2));
    }

    [Test]
    public void UnacceptedPrefabsProduceNothing()
    {
        Setup(1, false, 1024, 2, 0, 1);
        Assert.AreEqual(0, Run(16f, 2));
    }

    [Test]
    public void OverflowIsCountedButNotWritten()
    {
        Setup(1, true, 10, 2, 0, 1);
        Assert.AreEqual(32, Run(16f, 2));
        for (int i = 0; i < _output.Length; i++)
        {
            Assert.AreEqual(Prefab, _output[i].PrefabId);
            Assert.AreEqual(3f, _output[i].Position.z, 1e-6f);
        }
    }

    [Test]
    public void FractionalCellsRoundProbabilistically()
    {
        Setup(1, true, 4096, 2000, 0, 1);
        int produced = Run(0.5f, 2000);
        Assert.That(produced, Is.InRange(850, 1150));
    }

    [Test]
    public void EventsCarryChunkCellAndRing()
    {
        Setup(1, true, 64, 1, 2, 4);
        int produced = Run(16f, 1);
        Assert.AreEqual(4, produced);
        for (int i = 0; i < produced; i++)
        {
            Assert.AreEqual(0L, _output[i].Chunk);
            Assert.AreEqual(2, _output[i].Ring);
            Assert.That(_output[i].Cell, Is.InRange(0, ChunkData.TotalCells - 1));
        }
    }
}
