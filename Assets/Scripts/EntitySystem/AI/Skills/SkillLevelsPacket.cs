using Unity.Mathematics;
using Unity.Netcode;

public struct SkillLevelsPacket : INetworkSerializable
{
    public int Version;
    public byte Root;
    public byte Count;
    public ushort[] Levels;
    public ushort[] Peaks;

    public static SkillLevelsPacket Allocate(int count) => new()
    {
        Count = (byte)count,
        Levels = new ushort[count],
        Peaks = new ushort[count],
    };

    public static ushort Quantize(float value, float scale)
        => (ushort)math.clamp(math.round(math.max(value, 0f) * scale), 0f, ushort.MaxValue);

    public static float Dequantize(ushort value, float scale) => value / scale;

    public void Capture(SkillGraphStore store, int index, int version, float scale)
    {
        Version = version;
        Root = (byte)store.Root(index);
        Count = (byte)store.SkillCount;
        for (int v = 0; v < Levels.Length; v++)
        {
            Levels[v] = Quantize(store.Level(index, v), scale);
            Peaks[v] = Quantize(store.Peak(index, v), scale);
        }
    }

    public bool IsWellFormed(int skillCount)
        => Count == skillCount && Root < skillCount && Levels != null && Peaks != null
        && Levels.Length == skillCount && Peaks.Length == skillCount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Version);
        serializer.SerializeValue(ref Root);
        serializer.SerializeValue(ref Count);
        if (serializer.IsReader)
        {
            Levels = new ushort[Count];
            Peaks = new ushort[Count];
        }
        for (int v = 0; v < Levels.Length; v++) serializer.SerializeValue(ref Levels[v]);
        for (int v = 0; v < Peaks.Length; v++) serializer.SerializeValue(ref Peaks[v]);
    }
}
