using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public sealed class AgentPuppets : IDisposable
{
    private const float SnapDistance = 64f;

    private readonly GameObject _prefab;
    private readonly Transform  _parent;
    private readonly int        _capacity;

    private readonly Dictionary<int, int> _slotOf;
    private readonly int[]        _ids;
    private readonly Transform[]  _transforms;
    private readonly GameObject[] _objects;
    private readonly float3[]     _targets;
    private readonly float[]      _headings;
    private readonly double[]     _expire;
    private readonly GameObject[] _spare;
    private readonly byte[]       _read = new byte[AgentSnapshotCodec.MaxMessageSize];

    private int  _count;
    private int  _spareCount;
    private bool _registered;
    private bool _disposed;

    public int Count => _count;
    public int Rejected { get; private set; }
    public int Dropped  { get; private set; }

    public AgentPuppets(GameObject prefab, Transform parent, int capacity)
    {
        _prefab     = prefab;
        _parent     = parent;
        _capacity   = math.max(capacity, 1);
        _slotOf     = new Dictionary<int, int>(_capacity);
        _ids        = new int[_capacity];
        _transforms = new Transform[_capacity];
        _objects    = new GameObject[_capacity];
        _targets    = new float3[_capacity];
        _headings   = new float[_capacity];
        _expire     = new double[_capacity];
        _spare      = new GameObject[_capacity];
        Register();
    }

    private void Register()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (_registered || network == null || network.CustomMessagingManager == null) return;

        network.CustomMessagingManager.RegisterNamedMessageHandler(AgentSnapshotCodec.MessageName, OnMessage);
        _registered = true;
    }

    private void OnMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_disposed || senderClientId != NetworkManager.ServerClientId) return;

        int length = reader.Length;
        if (length < AgentSnapshotCodec.HeaderSize || length > AgentSnapshotCodec.MaxMessageSize)
        {
            Rejected++;
            return;
        }

        byte[] read = _read;
        reader.ReadBytesSafe(ref read, length);
        if (!AgentSnapshotCodec.TryReadHeader(read, length, out AgentSnapshotCodec.Header header))
        {
            Rejected++;
            return;
        }

        double expire = SimulationClock.TimeD + header.TtlMs * 0.001;
        for (int i = 0; i < header.Count; i++)
        {
            if (!AgentSnapshotCodec.TryReadEntry(read, i, in header, out int id, out float3 position, out byte yaw, out _))
            {
                Rejected++;
                continue;
            }

            if (!_slotOf.TryGetValue(id, out int slot))
            {
                slot = Acquire(id, position);
                if (slot < 0)
                {
                    Dropped++;
                    continue;
                }
            }

            _targets[slot] = position;
            _expire[slot]  = expire;
            if (yaw != AgentSnapshotCodec.YawNone) _headings[slot] = AgentSnapshotCodec.YawDegrees(yaw);
        }
    }

    public void Tick(double now, float dt, float smoothing)
    {
        if (_disposed) return;
        Register();

        float k = 1f - math.exp(-math.max(smoothing, 0f) * math.max(dt, 0f));
        for (int i = _count - 1; i >= 0; i--)
        {
            if (now > _expire[i])
            {
                Release(i);
                continue;
            }

            Transform tr = _transforms[i];
            float3 current = tr.position;
            float3 target  = _targets[i];
            float3 next    = math.distancesq(current, target) > SnapDistance * SnapDistance ? target : math.lerp(current, target, k);
            tr.SetPositionAndRotation(next, Quaternion.Euler(0f, _headings[i], 0f));
        }
    }

    private int Acquire(int id, float3 position)
    {
        if (_count >= _capacity) return -1;

        GameObject go;
        if (_spareCount > 0)
        {
            go = _spare[--_spareCount];
            _spare[_spareCount] = null;
        }
        else
        {
            if (_prefab == null) return -1;
            go = UnityEngine.Object.Instantiate(_prefab, _parent);
        }

        int slot = _count++;
        Transform tr = go.transform;
        tr.SetPositionAndRotation(position, Quaternion.identity);
        go.SetActive(true);

        _ids[slot]        = id;
        _objects[slot]    = go;
        _transforms[slot] = tr;
        _targets[slot]    = position;
        _headings[slot]   = 0f;
        _expire[slot]     = double.NegativeInfinity;
        _slotOf[id]       = slot;
        return slot;
    }

    private void Release(int slot)
    {
        GameObject go = _objects[slot];
        _slotOf.Remove(_ids[slot]);
        if (go != null)
        {
            go.SetActive(false);
            if (_spareCount < _spare.Length) _spare[_spareCount++] = go;
            else UnityEngine.Object.Destroy(go);
        }

        int last = --_count;
        if (slot != last)
        {
            _ids[slot]        = _ids[last];
            _objects[slot]    = _objects[last];
            _transforms[slot] = _transforms[last];
            _targets[slot]    = _targets[last];
            _headings[slot]   = _headings[last];
            _expire[slot]     = _expire[last];
            _slotOf[_ids[slot]] = slot;
        }
        _objects[last]    = null;
        _transforms[last] = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        NetworkManager network = NetworkManager.Singleton;
        if (_registered && network != null && network.CustomMessagingManager != null)
            network.CustomMessagingManager.UnregisterNamedMessageHandler(AgentSnapshotCodec.MessageName);
        _registered = false;

        for (int i = 0; i < _count; i++)
            if (_objects[i] != null) UnityEngine.Object.Destroy(_objects[i]);
        for (int i = 0; i < _spareCount; i++)
            if (_spare[i] != null) UnityEngine.Object.Destroy(_spare[i]);
        _count = 0;
        _spareCount = 0;
        _slotOf.Clear();
    }
}
