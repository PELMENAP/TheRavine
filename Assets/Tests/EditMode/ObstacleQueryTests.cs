using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Generator;

public class ObstacleQueryTests
{
    private const int TreeId = 1;
    private const int BlockId = 2;
    private const float Radius = 0.5f;

    private struct SingleChunk : IObstacleLookup
    {
        public ChunkData Chunk;

        public bool TryGetObstacles(long chunkKey, out ObstacleChunk chunk)
        {
            if (chunkKey == MapGenerator.CellToChunk(0, 0))
            {
                chunk = Chunk.ObstacleView();
                return true;
            }
            chunk = default;
            return false;
        }
    }

    private ChunkData _chunk;
    private NativeArray<CollisionShape> _shapes;
    private ObstacleSet _set;
    private SingleChunk _lookup;
    private float2 _probe;

    [SetUp]
    public void SetUp()
    {
        _chunk = new ChunkData();
        _shapes = new NativeArray<CollisionShape>(3, Allocator.Persistent);
        _shapes[TreeId] = CollisionShape.Cylinder(1f, 3f);
        _shapes[BlockId] = CollisionShape.Box(new float2(0.5f, 0.5f), 1f);
        _set = new ObstacleSet
        {
            Shapes = _shapes,
            MaxExtent = math.max(_shapes[TreeId].Extent, _shapes[BlockId].Extent)
        };
        _lookup = new SingleChunk { Chunk = _chunk };
    }

    [TearDown]
    public void TearDown()
    {
        _chunk.Dispose();
        _shapes.Dispose();
    }

    private void Place(int cellX, int cellZ, int id, float y = 0f, int baseLevel = 0)
    {
        var info = new ObjectInstInfo(new Vector3(cellX * MapGenerator.scale, y, cellZ * MapGenerator.scale), id, 1);
        Assert.IsTrue(_chunk.TryAddObject(MapGenerator.CellToLocalIdx(cellX, cellZ), in info, default, 1, baseLevel));
    }

    private bool Resolve(ref float2 position, float feet, float head, out float2 normal)
        => ObstacleQuery.Resolve(ref _lookup, in _set, ref position, Radius, feet, head, out normal);

    [Test]
    public void CylinderPushesTheBodyToTheSumOfRadii()
    {
        Place(5, 5, TreeId);
        float2 p = new float2(10.5f, 10f);

        Assert.IsTrue(Resolve(ref p, 0f, 2f, out float2 n));
        Assert.AreEqual(11.5f, p.x, 1e-4f);
        Assert.AreEqual(10f, p.y, 1e-4f);
        Assert.AreEqual(1f, n.x, 1e-4f);
    }

    [Test]
    public void BodyOutsideTheSumOfRadiiIsNotMoved()
    {
        Place(5, 5, TreeId);
        float2 p = new float2(11.6f, 10f);

        Assert.IsFalse(Resolve(ref p, 0f, 2f, out _));
        Assert.AreEqual(11.6f, p.x, 1e-6f);
    }

    [Test]
    public void BodyAboveOrBelowTheShapeIsNotMoved()
    {
        Place(5, 5, TreeId);
        float2 above = new float2(10.5f, 10f);
        float2 below = new float2(10.5f, 10f);

        Assert.IsFalse(Resolve(ref above, 3f, 5f, out _));
        Assert.IsFalse(Resolve(ref below, -2f, 0f, out _));
    }

    [Test]
    public void BoxPushesAlongTheFaceNormal()
    {
        Place(5, 5, BlockId);
        float2 p = new float2(10.8f, 10.1f);

        Assert.IsTrue(Resolve(ref p, 0f, 2f, out float2 n));
        Assert.AreEqual(11f, p.x, 1e-4f);
        Assert.AreEqual(10.1f, p.y, 1e-4f);
        Assert.AreEqual(1f, n.x, 1e-4f);
    }

    [Test]
    public void BodyInsideABoxLeavesThroughTheNearestFace()
    {
        Place(5, 5, BlockId);
        float2 p = new float2(10.1f, 9.7f);

        Assert.IsTrue(Resolve(ref p, 0f, 2f, out float2 n));
        Assert.AreEqual(10.1f, p.x, 1e-4f);
        Assert.AreEqual(9f, p.y, 1e-4f);
        Assert.AreEqual(-1f, n.y, 1e-4f);
    }

    [Test]
    public void ElevatedBlockStopsOnlyABodyThatReachesItsHeight()
    {
        Place(5, 5, BlockId, 1f, 1);
        float2 tall = new float2(10.8f, 10f);
        float2 low = new float2(10.8f, 10f);

        Assert.IsTrue(Resolve(ref tall, 0f, 2f, out _));
        Assert.AreEqual(11f, tall.x, 1e-4f);
        Assert.IsFalse(Resolve(ref low, 0f, 0.9f, out _));
    }

    [Test]
    public void RemovedObjectNoLongerBlocks()
    {
        Place(5, 5, TreeId);
        Place(7, 5, BlockId, 1f, 1);
        Assert.IsTrue(_chunk.RemoveObject(MapGenerator.CellToLocalIdx(5, 5)));
        Assert.IsTrue(_chunk.RemoveObject(MapGenerator.CellToLocalIdx(7, 5), 1));

        float2 p = new float2(10.5f, 10f);
        float2 q = new float2(14.8f, 10f);
        Assert.IsFalse(Resolve(ref p, 0f, 2f, out _));
        Assert.IsFalse(Resolve(ref q, 0f, 2f, out _));
        Assert.AreEqual(0, _chunk.ElevatedCount[0]);
    }

    [Test]
    public void ResolveDoesNotAllocate()
    {
        Place(5, 5, TreeId);
        Place(7, 5, BlockId, 1f, 1);

        AllocationAssert.Zero(Probe, 200, "ObstacleQuery.Resolve");
    }

    private void Probe()
    {
        _probe = new float2(10.5f, 10f);
        Resolve(ref _probe, 0f, 2f, out _);
        _probe = new float2(14.8f, 10f);
        Resolve(ref _probe, 0f, 2f, out _);
    }
}
