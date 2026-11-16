using Unity.Mathematics;
using Unity.Netcode;

public struct SkillTopologyPacket : INetworkSerializable
{
    public int Version;
    public int Revision;
    public byte Auto;
    public byte Count;
    public byte BridgeCount;
    public byte Spent;
    public float PhiPeak;
    public ulong IslandMask;
    public ulong PrunedMask;
    public float4 Forest;
    public float2 Origin;
    public float4 Plast;
    public ushort[] Bridges;
    public ulong[] Rows;

    public static SkillTopologyPacket Allocate(int count) => new()
    {
        Count = (byte)count,
        Rows = new ulong[count],
        Bridges = new ushort[SkillGraphTopology.MaxPrunedBridges],
    };

    public void Capture(SkillGraphTopology topology, int version, bool auto)
    {
        Version = version;
        Revision = topology.Revision;
        Auto = auto ? (byte)1 : (byte)0;
        Count = (byte)topology.Count;
        Spent = (byte)topology.SpentEdgePoints;
        PhiPeak = topology.PhiPeak;
        IslandMask = topology.IslandMask;
        PrunedMask = topology.PrunedMask;
        Forest = topology.ForestGenes;
        BridgeCount = (byte)topology.PrunedBridgeCount;
        for (int i = 0; i < Bridges.Length; i++) Bridges[i] = i < BridgeCount ? topology.PrunedBridgeAt(i) : (ushort)0;
        for (int v = 0; v < Rows.Length; v++) Rows[v] = topology.ActiveRow(v);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Version);
        serializer.SerializeValue(ref Revision);
        serializer.SerializeValue(ref Auto);
        serializer.SerializeValue(ref Count);
        serializer.SerializeValue(ref BridgeCount);
        serializer.SerializeValue(ref Spent);
        serializer.SerializeValue(ref PhiPeak);
        serializer.SerializeValue(ref IslandMask);
        serializer.SerializeValue(ref PrunedMask);
        serializer.SerializeValue(ref Forest.x);
        serializer.SerializeValue(ref Forest.y);
        serializer.SerializeValue(ref Forest.z);
        serializer.SerializeValue(ref Forest.w);
        serializer.SerializeValue(ref Origin.x);
        serializer.SerializeValue(ref Origin.y);
        serializer.SerializeValue(ref Plast.x);
        serializer.SerializeValue(ref Plast.y);
        serializer.SerializeValue(ref Plast.z);
        serializer.SerializeValue(ref Plast.w);
        if (serializer.IsReader)
        {
            Rows = new ulong[Count];
            Bridges = new ushort[SkillGraphTopology.MaxPrunedBridges];
        }
        for (int i = 0; i < Bridges.Length; i++) serializer.SerializeValue(ref Bridges[i]);
        for (int v = 0; v < Rows.Length; v++) serializer.SerializeValue(ref Rows[v]);
    }
}
