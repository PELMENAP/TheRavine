using NUnit.Framework;
using Unity.Mathematics;

public class AgentSnapshotCodecTests
{
    private static byte[] Build(int count, float3 origin, int ttlMs = 500)
    {
        var buffer = new byte[AgentSnapshotCodec.MaxMessageSize];
        for (int i = 0; i < count; i++)
            AgentSnapshotCodec.WriteEntry(buffer, i, i + 1, origin, origin + new float3(i, i * 0.5f, -i), (byte)i, (byte)(i % 5));
        AgentSnapshotCodec.WriteHeader(buffer, count, ttlMs, 7u, origin);
        return buffer;
    }

    private static int LengthOf(int count) => AgentSnapshotCodec.HeaderSize + count * AgentSnapshotCodec.EntrySize;

    [Test]
    public void RoundTripKeepsIdsPositionsAndHeading()
    {
        var origin = new float3(1000.5f, 12f, -2048.25f);
        var buffer = Build(5, origin);

        Assert.IsTrue(AgentSnapshotCodec.TryReadHeader(buffer, LengthOf(5), out var header));
        Assert.AreEqual(5, header.Count);
        Assert.AreEqual(500, header.TtlMs);
        Assert.AreEqual(7u, header.Tick);
        Assert.AreEqual(origin, header.Origin);

        for (int i = 0; i < 5; i++)
        {
            Assert.IsTrue(AgentSnapshotCodec.TryReadEntry(buffer, i, in header, out int id, out float3 p, out byte yaw, out byte colony));
            Assert.AreEqual(i + 1, id);
            Assert.AreEqual(origin.x + i, p.x, 1f / AgentSnapshotCodec.PositionScale);
            Assert.AreEqual(origin.y + i * 0.5f, p.y, 1f / AgentSnapshotCodec.PositionScale);
            Assert.AreEqual(origin.z - i, p.z, 1f / AgentSnapshotCodec.PositionScale);
            Assert.AreEqual((byte)i, yaw);
            Assert.AreEqual((byte)(i % 5), colony);
        }
    }

    [Test]
    public void FullMessageFitsTheSingleDatagramBudget()
    {
        Assert.LessOrEqual(AgentSnapshotCodec.MaxMessageSize, 1200);
        var buffer = Build(AgentSnapshotCodec.MaxEntries, float3.zero);
        Assert.IsTrue(AgentSnapshotCodec.TryReadHeader(buffer, LengthOf(AgentSnapshotCodec.MaxEntries), out _));
    }

    [Test]
    public void OffsetBeyondTheQuantisationRangeSaturatesInsteadOfWrapping()
    {
        var origin = float3.zero;
        var buffer = new byte[AgentSnapshotCodec.MaxMessageSize];
        AgentSnapshotCodec.WriteEntry(buffer, 0, 1, origin, new float3(5000f, -5000f, 0f), 0, 0);
        AgentSnapshotCodec.WriteHeader(buffer, 1, 500, 1u, origin);

        Assert.IsTrue(AgentSnapshotCodec.TryReadHeader(buffer, LengthOf(1), out var header));
        Assert.IsTrue(AgentSnapshotCodec.TryReadEntry(buffer, 0, in header, out _, out float3 p, out _, out _));
        Assert.AreEqual(AgentSnapshotCodec.MaxOffset, p.x, 0.1f);
        Assert.AreEqual(-2048f, p.y, 0.1f);
    }

    [Test]
    public void YawQuantisationCoversTheCircleAndMarksRest()
    {
        Assert.AreEqual(AgentSnapshotCodec.YawNone, AgentSnapshotCodec.QuantizeYaw(float2.zero));
        Assert.AreEqual(0f, AgentSnapshotCodec.YawDegrees(AgentSnapshotCodec.QuantizeYaw(new float2(0f, 1f))), 1.5f);
        Assert.AreEqual(90f, AgentSnapshotCodec.YawDegrees(AgentSnapshotCodec.QuantizeYaw(new float2(1f, 0f))), 1.5f);
        Assert.AreEqual(270f, AgentSnapshotCodec.YawDegrees(AgentSnapshotCodec.QuantizeYaw(new float2(-1f, 0f))), 1.5f);
        for (int i = 0; i < 360; i += 7)
        {
            float rad = math.radians(i);
            byte q = AgentSnapshotCodec.QuantizeYaw(new float2(math.sin(rad), math.cos(rad)));
            Assert.Less(q, AgentSnapshotCodec.YawNone);
        }
    }

    [Test]
    public void MalformedHeadersAreRefused()
    {
        var good = Build(3, float3.zero);
        Assert.IsTrue(AgentSnapshotCodec.TryReadHeader(good, LengthOf(3), out _));

        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(good, LengthOf(3) - 1, out _), "truncated");
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(good, LengthOf(3) + 1, out _), "trailing bytes");
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(good, AgentSnapshotCodec.HeaderSize - 1, out _), "short");
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(good, 0, out _), "empty");
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(null, 10, out _), "null");
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(good, int.MaxValue, out _), "huge length");
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(good, int.MinValue, out _), "negative length");

        var version = (byte[])good.Clone();
        version[0] = (byte)(AgentSnapshotCodec.Version + 1);
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(version, LengthOf(3), out _), "version");

        var count = (byte[])good.Clone();
        count[1] = AgentSnapshotCodec.MaxEntries + 1;
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(count, LengthOf(AgentSnapshotCodec.MaxEntries + 1), out _), "count above the maximum");

        var mismatch = (byte[])good.Clone();
        mismatch[1] = 4;
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(mismatch, LengthOf(3), out _), "count and length disagree");
    }

    [Test]
    public void TtlOutsideTheBoundsIsRefused()
    {
        var low = Build(1, float3.zero);
        low[2] = 1; low[3] = 0;
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(low, LengthOf(1), out _));

        var high = Build(1, float3.zero);
        high[2] = 0xFF; high[3] = 0xFF;
        Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(high, LengthOf(1), out _));
    }

    [Test]
    public void NonFiniteOrHugeOriginIsRefused()
    {
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1.0e12f })
        {
            var buffer = Build(1, new float3(bad, 0f, 0f));
            Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(buffer, LengthOf(1), out _), $"origin x {bad}");
            buffer = Build(1, new float3(0f, 0f, bad));
            Assert.IsFalse(AgentSnapshotCodec.TryReadHeader(buffer, LengthOf(1), out _), $"origin z {bad}");
        }
    }

    [Test]
    public void EntryWithNonPositiveIdOrOutOfRangeIndexIsRefused()
    {
        var buffer = Build(2, float3.zero);
        Assert.IsTrue(AgentSnapshotCodec.TryReadHeader(buffer, LengthOf(2), out var header));

        Assert.IsFalse(AgentSnapshotCodec.TryReadEntry(buffer, 2, in header, out _, out _, out _, out _));
        Assert.IsFalse(AgentSnapshotCodec.TryReadEntry(buffer, -1, in header, out _, out _, out _, out _));

        int o = AgentSnapshotCodec.HeaderSize;
        buffer[o] = 0; buffer[o + 1] = 0; buffer[o + 2] = 0; buffer[o + 3] = 0;
        Assert.IsFalse(AgentSnapshotCodec.TryReadEntry(buffer, 0, in header, out _, out _, out _, out _));

        buffer[o] = 0xFF; buffer[o + 1] = 0xFF; buffer[o + 2] = 0xFF; buffer[o + 3] = 0xFF;
        Assert.IsFalse(AgentSnapshotCodec.TryReadEntry(buffer, 0, in header, out _, out _, out _, out _));
    }

    [Test]
    public void RandomBytesNeverThrowAndAreAlmostAlwaysRefused()
    {
        var rng = new System.Random(11);
        var buffer = new byte[AgentSnapshotCodec.MaxMessageSize];
        int accepted = 0;
        for (int round = 0; round < 5000; round++)
        {
            rng.NextBytes(buffer);
            int length = rng.Next(0, buffer.Length + 1);
            if (!AgentSnapshotCodec.TryReadHeader(buffer, length, out var header)) continue;
            accepted++;
            for (int i = 0; i < header.Count; i++)
            {
                if (!AgentSnapshotCodec.TryReadEntry(buffer, i, in header, out _, out float3 p, out _, out _)) continue;
                Assert.IsTrue(math.all(math.isfinite(p)));
            }
        }
        Assert.Less(accepted, 50);
    }

    [Test]
    public void WriteAndReadDoNotAllocate()
    {
        var buffer = new byte[AgentSnapshotCodec.MaxMessageSize];
        var origin = new float3(10f, 2f, 30f);
        int sink = 0;
        AllocationAssert.Zero(() =>
        {
            for (int i = 0; i < AgentSnapshotCodec.MaxEntries; i++)
                AgentSnapshotCodec.WriteEntry(buffer, i, i + 1, origin, origin + new float3(i, 0f, i), AgentSnapshotCodec.QuantizeYaw(new float2(i, 1f)), 1);
            AgentSnapshotCodec.WriteHeader(buffer, AgentSnapshotCodec.MaxEntries, 500, 3u, origin);

            if (!AgentSnapshotCodec.TryReadHeader(buffer, AgentSnapshotCodec.MaxMessageSize, out var header)) return;
            for (int i = 0; i < header.Count; i++)
                if (AgentSnapshotCodec.TryReadEntry(buffer, i, in header, out int id, out _, out _, out _)) sink += id;
        }, 64, "AgentSnapshotCodec");
        Assert.Greater(sink, 0);
    }
}
