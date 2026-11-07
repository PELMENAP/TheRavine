using System;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.EntityControl;
using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public sealed class PlayerPresence
{
    private AEntity[]           _players    = Array.Empty<AEntity>();
    private Transform[]         _transforms = Array.Empty<Transform>();
    private StatsComponent[]    _stats      = Array.Empty<StatsComponent>();
    private VirologyComponent[] _virology   = Array.Empty<VirologyComponent>();
    private float2[]            _positions  = Array.Empty<float2>();
    private int _count;

    public int Count => _count;

    public event Action<Transform, float, EntityModel> Hit;

    public void Refresh()
    {
        var players = ServiceLocator.Players.GetAllPlayers();
        int n = players.Count;
        EnsureCapacity(n);

        int k = 0;
        for (int i = 0; i < n; i++)
        {
            var p = players[i];
            if (p == null || p.IsDisposed || !p.IsActive.Value) continue;

            if (!ReferenceEquals(_players[k], p) || _transforms[k] == null)
            {
                _players[k]    = p;
                _transforms[k] = p.GetEntityComponent<TransformComponent>()?.GetEntityTransform();
                _stats[k]      = p.GetEntityComponent<StatsComponent>();
                _virology[k]   = p.GetEntityComponent<VirologyComponent>();
            }

            var t = _transforms[k];
            if (t == null) continue;

            Vector3 w = t.position;
            _positions[k] = new float2(w.x, w.z);
            k++;
        }

        for (int i = k; i < _count; i++)
        {
            _players[i]    = null;
            _transforms[i] = null;
            _stats[i]      = null;
            _virology[i]   = null;
        }
        _count = k;
    }

    public int MinChunkDistance(int chunkX, int chunkZ)
    {
        int best = int.MaxValue;
        for (int i = 0; i < _count; i++)
        {
            int dx = math.abs(MapGenerator.ChunkCoord(_positions[i].x) - chunkX);
            int dz = math.abs(MapGenerator.ChunkCoord(_positions[i].y) - chunkZ);
            best = math.min(best, math.max(dx, dz));
        }
        return best;
    }

    public bool TryFindNearest(float2 from, float radius, out float2 position, out float distance, out Transform transform,
        out StatsComponent stats, out VirologyComponent virology)
    {
        position  = default;
        transform = null;
        stats     = null;
        virology  = null;
        distance  = -1f;
        float best = radius * radius;
        int   idx  = -1;
        for (int i = 0; i < _count; i++)
        {
            float d2 = math.distancesq(from, _positions[i]);
            if (d2 > best) continue;
            best = d2;
            idx  = i;
        }
        if (idx < 0) return false;

        position  = _positions[idx];
        transform = _transforms[idx];
        stats     = _stats[idx];
        virology  = _virology[idx];
        distance  = math.sqrt(best);
        return true;
    }

    public StatsComponent StatsOf(Transform player)
    {
        for (int i = 0; i < _count; i++)
            if (ReferenceEquals(_transforms[i], player)) return _stats[i];
        return null;
    }

    public void ReportHit(Transform player, float damage, EntityModel attacker) => Hit?.Invoke(player, damage, attacker);

    private void EnsureCapacity(int n)
    {
        if (n <= _players.Length) return;
        int cap = math.max(n, math.max(_players.Length << 1, 4));
        Array.Resize(ref _players, cap);
        Array.Resize(ref _transforms, cap);
        Array.Resize(ref _positions, cap);
        Array.Resize(ref _stats, cap);
        Array.Resize(ref _virology, cap);
    }
}
