using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Extensions;
using TheRavine.Generator;

public class GrassTrampleFieldTests
{
    private static GrassTrampleField Create(int capacity)
    {
        var field = new GrassTrampleField(null, new GrassTrampleSettings { stampCapacity = capacity });
        field.Recenter(Position2Int.Pack(1, 1));
        return field;
    }

    [Test]
    public void StampsBeforeFirstRecenterAreDropped()
    {
        var field = new GrassTrampleField(null, new GrassTrampleSettings { stampCapacity = 8 });
        try
        {
            field.Stamp(new float2(10f, 10f), 1f, 0.5f);
            Assert.AreEqual(0, field.StampCount);
        }
        finally
        {
            field.Dispose();
        }
    }

    [Test]
    public void OnlyStampsInsideTheWindowAreKept()
    {
        var field = Create(8);
        try
        {
            field.Stamp(new float2(100f, 100f), 1f, 0.5f);
            field.Stamp(new float2(5000f, 100f), 1f, 0.5f);
            field.Stamp(new float2(-300f, -300f), 1f, 0.5f);
            Assert.AreEqual(1, field.StampCount);
        }
        finally
        {
            field.Dispose();
        }
    }

    [Test]
    public void OverflowIsDroppedAtCapacity()
    {
        var field = Create(4);
        try
        {
            for (int i = 0; i < 10; i++) field.Stamp(new float2(100f + i, 100f), 1f, 0.5f);
            Assert.AreEqual(4, field.StampCount);
        }
        finally
        {
            field.Dispose();
        }
    }

    [Test]
    public void StampingDoesNotAllocate()
    {
        var field = Create(4096);
        try
        {
            AllocationAssert.Zero(() =>
            {
                field.Stamp(new float2(100f, 100f), 1f, 0.5f);
                field.StampCreature(new float2(120f, 90f), new float2(2f, 1f), 100f, 0.016f);
            }, 64, "GrassTrampleField.Stamp");
        }
        finally
        {
            field.Dispose();
        }
    }

    [Test]
    public void WindowFollowsRecenter()
    {
        var field = Create(8);
        try
        {
            Assert.AreEqual(new float2(0f, 0f), field.Origin);
            field.Recenter(Position2Int.Pack(2, 1));
            Assert.AreEqual(new float2(MapGenerator.chunkSize, 0f), field.Origin);
        }
        finally
        {
            field.Dispose();
        }
    }
}
