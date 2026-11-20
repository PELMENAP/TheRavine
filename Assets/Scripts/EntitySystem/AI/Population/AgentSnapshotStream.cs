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
    private readonly int[] _nextInCell;
    private readonly Dictionary<int2, int> _cellHeads;

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
        _nextInCell = new int[_candidates.Length];
        _cellHeads = new Dictionary<int2, int>(_candidates.Length);
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
        bool indexed = false;
        for (int c = 0; c < ids.Count; c++)
        {
            ulong clientId = ids[c];
            if (clientId == NetworkManager.ServerClientId) continue;

            if (!TryGetCenter(network, clientId, out float3 center))
            {
                ClientsWithoutPlayer++;
                continue;
            }

            if (!indexed)
            {
                IndexPositions(positions, count, radius);
                indexed = true;
            }
            SendTo(network, clientId, center, positions, motion, entities, radius, budget, interval);
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

    private void IndexPositions(NativeArray<float3> positions, int count, float radius)
    {
        _cellHeads.Clear();
        for (int i = count - 1; i >= 0; i--)
        {
            int2 cell = (int2)math.floor(positions[i].xz / radius);
            _nextInCell[i] = _cellHeads.TryGetValue(cell, out int head) ? head : -1;
            _cellHeads[cell] = i;
        }
    }

    private void SendTo(NetworkManager network, ulong clientId, float3 center, NativeArray<float3> positions,
        MotionSystem motion, List<EntityModel> entities, float radius, int budget, float interval)
    {
        float r2 = radius * radius;
        int found = 0;
        int2 origin = (int2)math.floor(center.xz / radius);
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (!_cellHeads.TryGetValue(origin + new int2(dx, dz), out int head)) continue;
            for (int i = head; i >= 0; i = _nextInCell[i])
                if (math.distancesq(positions[i].xz, center.xz) <= r2) _candidates[found++] = i;
        }
        if (found == 0) return;

        int slot   = SlotOf(clientId, true);
        int start  = _cursors[slot] % found;
        int toSend = math.min(found, budget);
        _cursors[slot] = (start + toSend) % found;

        int cycles = (found + budget - 1) / budget;
        int ttlMs  = (int)System.Math.Min(int.MaxValue, System.Math.Ceiling(math.max(interval, 0.02f) * 1000d * (cycles + 2d)));

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
        writer.WriteBytesSafe(_buffer, length);
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
