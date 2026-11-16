using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public sealed class AgentSnapshotStream
{
    private const int InitialClients = 8;

    private readonly byte[] _buffer = new byte[AgentSnapshotCodec.MaxMessageSize];
    private readonly int[]  _candidates;

    private ulong[] _clients = new ulong[InitialClients];
    private int[]   _cursors = new int[InitialClients];
    private int     _clientCount;

    private double _nextTime;
    private uint   _tick;

    public int  MessagesSent { get; private set; }
    public int  EntriesSent  { get; private set; }
    public int  ClientsWithoutPlayer { get; private set; }

    public AgentSnapshotStream(int capacity)
    {
        _candidates = new int[math.max(capacity, 1)];
    }

    public void Tick(double now, MotionSystem motion, List<EntityModel> entities, float interval, float radius, int budget)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening || !network.IsServer) return;
        if (now < _nextTime) return;
        _nextTime = now + math.max(interval, 0.02f);

        int count = math.min(entities.Count, _candidates.Length);
        NativeArray<float3> positions = motion.Positions;
        if (count == 0 || !positions.IsCreated) return;

        radius = math.clamp(radius, 1f, AgentSnapshotCodec.MaxOffset * 0.95f);
        budget = math.clamp(budget, 1, 4096);
        _tick++;

        for (int i = _clientCount - 1; i >= 0; i--)
            if (!network.ConnectedClients.ContainsKey(_clients[i])) Forget(_clients[i]);

        IReadOnlyList<ulong> ids = network.ConnectedClientsIds;
        ClientsWithoutPlayer = 0;
        for (int c = 0; c < ids.Count; c++)
        {
            ulong clientId = ids[c];
            if (clientId == NetworkManager.ServerClientId) continue;

            if (!TryGetCenter(network, clientId, out float3 center))
            {
                ClientsWithoutPlayer++;
                continue;
            }

            SendTo(network, clientId, center, positions, motion, entities, count, radius, budget, interval);
        }
    }

    public void Forget(ulong clientId)
    {
        int slot = SlotOf(clientId, false);
        if (slot < 0) return;

        _clientCount--;
        _clients[slot] = _clients[_clientCount];
        _cursors[slot] = _cursors[_clientCount];
    }

    private void SendTo(NetworkManager network, ulong clientId, float3 center, NativeArray<float3> positions,
        MotionSystem motion, List<EntityModel> entities, int count, float radius, int budget, float interval)
    {
        float r2 = radius * radius;
        int found = 0;
        for (int i = 0; i < count; i++)
        {
            float3 p = positions[i];
            float dx = p.x - center.x;
            float dz = p.z - center.z;
            if (dx * dx + dz * dz <= r2) _candidates[found++] = i;
        }
        if (found == 0) return;

        int slot   = SlotOf(clientId, true);
        int start  = _cursors[slot] % found;
        int toSend = math.min(found, budget);
        _cursors[slot] = (start + toSend) % found;

        int cycles = (found + budget - 1) / budget;
        int ttlMs  = (int)(math.max(interval, 0.02f) * 1000f * (cycles + 2));

        int filled = 0;
        for (int k = 0; k < toSend; k++)
        {
            int idx = _candidates[(start + k) % found];
            EntityModel e = entities[idx];
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;

            float2 v = motion.VelocityBySlot(idx);
            AgentSnapshotCodec.WriteEntry(_buffer, filled, e.EntityId, center, positions[idx],
                AgentSnapshotCodec.QuantizeYaw(v), (byte)math.min(e.ColonyId, 255));
            if (++filled == AgentSnapshotCodec.MaxEntries)
            {
                Flush(network, clientId, filled, ttlMs, center);
                filled = 0;
            }
        }

        if (filled > 0) Flush(network, clientId, filled, ttlMs, center);
    }

    private void Flush(NetworkManager network, ulong clientId, int filled, int ttlMs, float3 center)
    {
        AgentSnapshotCodec.WriteHeader(_buffer, filled, ttlMs, _tick, center);
        int length = AgentSnapshotCodec.HeaderSize + filled * AgentSnapshotCodec.EntrySize;

        using var writer = new FastBufferWriter(length, Allocator.Temp);
        writer.WriteBytes(_buffer, length);
        network.CustomMessagingManager.SendNamedMessage(AgentSnapshotCodec.MessageName, clientId, writer, NetworkDelivery.UnreliableSequenced);

        MessagesSent++;
        EntriesSent += filled;
    }

    private static bool TryGetCenter(NetworkManager network, ulong clientId, out float3 center)
    {
        center = default;
        if (!network.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client == null) return false;

        NetworkObject player = client.PlayerObject;
        if (player == null) return false;

        Vector3 p = player.transform.position;
        center = new float3(p.x, p.y, p.z);
        return math.all(math.isfinite(center));
    }

    private int SlotOf(ulong clientId, bool create)
    {
        for (int i = 0; i < _clientCount; i++)
            if (_clients[i] == clientId) return i;
        if (!create) return -1;

        if (_clientCount == _clients.Length)
        {
            System.Array.Resize(ref _clients, _clientCount << 1);
            System.Array.Resize(ref _cursors, _clientCount << 1);
        }
        _clients[_clientCount] = clientId;
        _cursors[_clientCount] = 0;
        return _clientCount++;
    }
}
