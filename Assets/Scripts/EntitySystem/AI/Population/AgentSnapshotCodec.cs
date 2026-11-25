using Unity.Mathematics;

public static class AgentSnapshotCodec
{
    public const string MessageName = "ravine.agents";
    public const byte   Version     = 2;

    public const int HeaderSize     = 22;
    public const int EntrySize      = 12;
    public const int MaxEntries     = 64;
    public const int MaxMessageSize = HeaderSize + MaxEntries * EntrySize;

    public const float PositionScale = 16f;
    public const float MaxOffset     = short.MaxValue / PositionScale;
    public const float MaxOrigin     = 1.0e7f;
    public const byte  YawNone       = 255;
    public const int   MinTtlMs      = 100;

    public struct Header
    {
        public byte   Count;
        public int TtlMs;
        public uint   Tick;
        public float3 Origin;
    }

    public static void WriteHeader(byte[] buffer, int count, int ttlMs, uint tick, float3 origin)
    {
        buffer[0] = Version;
        buffer[1] = (byte)count;
        WriteU32(buffer, 2, (uint)math.max(ttlMs, MinTtlMs));
        WriteU32(buffer, 6, tick);
        WriteF32(buffer, 10, origin.x);
        WriteF32(buffer, 14, origin.y);
        WriteF32(buffer, 18, origin.z);
    }

    public static void WriteEntry(byte[] buffer, int index, int id, float3 origin, float3 position, byte yaw, byte colony)
    {
        int o = HeaderSize + index * EntrySize;
        WriteU32(buffer, o, (uint)id);
        WriteI16(buffer, o + 4, Quantize(position.x - origin.x));
        WriteI16(buffer, o + 6, Quantize(position.y - origin.y));
        WriteI16(buffer, o + 8, Quantize(position.z - origin.z));
        buffer[o + 10] = yaw;
        buffer[o + 11] = colony;
    }

    public static bool TryReadHeader(byte[] buffer, int length, out Header header)
    {
        header = default;
        if (buffer == null || length < HeaderSize || length > buffer.Length || buffer[0] != Version) return false;

        int count = buffer[1];
        if (count > MaxEntries || length != HeaderSize + count * EntrySize) return false;

        int ttl = (int)ReadU32(buffer, 2);
        if (ttl < MinTtlMs) return false;

        float3 origin = new float3(ReadF32(buffer, 10), ReadF32(buffer, 14), ReadF32(buffer, 18));
        if (!math.all(math.isfinite(origin)) || math.any(math.abs(origin) > MaxOrigin)) return false;

        header = new Header { Count = (byte)count, TtlMs = ttl, Tick = ReadU32(buffer, 6), Origin = origin };
        return true;
    }

    public static bool TryReadEntry(byte[] buffer, int index, in Header header, out int id, out float3 position, out byte yaw, out byte colony)
    {
        id       = 0;
        position = default;
        yaw      = YawNone;
        colony   = 0;
        if ((uint)index >= (uint)header.Count) return false;

        int o = HeaderSize + index * EntrySize;
        id = (int)ReadU32(buffer, o);
        if (id <= 0) return false;

        position = header.Origin + new float3(ReadI16(buffer, o + 4), ReadI16(buffer, o + 6), ReadI16(buffer, o + 8)) * (1f / PositionScale);
        yaw      = buffer[o + 10];
        colony   = buffer[o + 11];
        return true;
    }

    public static byte QuantizeYaw(float2 velocity)
    {
        if (math.lengthsq(velocity) < 0.01f) return YawNone;
        float turns = math.atan2(velocity.x, velocity.y) * (1f / (2f * math.PI));
        turns -= math.floor(turns);
        return (byte)math.min(254, (int)(turns * 255f));
    }

    public static float YawDegrees(byte quantized) => quantized * (360f / 255f);

    private static short Quantize(float offset)
        => (short)math.clamp((int)math.round(offset * PositionScale), short.MinValue, short.MaxValue);

    private static void WriteU16(byte[] b, int o, ushort v)
    {
        b[o]     = (byte)v;
        b[o + 1] = (byte)(v >> 8);
    }

    private static void WriteI16(byte[] b, int o, short v) => WriteU16(b, o, (ushort)v);

    private static void WriteU32(byte[] b, int o, uint v)
    {
        b[o]     = (byte)v;
        b[o + 1] = (byte)(v >> 8);
        b[o + 2] = (byte)(v >> 16);
        b[o + 3] = (byte)(v >> 24);
    }

    private static void WriteF32(byte[] b, int o, float v) => WriteU32(b, o, math.asuint(v));

    private static ushort ReadU16(byte[] b, int o) => (ushort)(b[o] | (b[o + 1] << 8));

    private static short ReadI16(byte[] b, int o) => (short)ReadU16(b, o);

    private static uint ReadU32(byte[] b, int o)
        => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

    private static float ReadF32(byte[] b, int o) => math.asfloat(ReadU32(b, o));
}
