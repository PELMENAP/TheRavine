using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

public sealed class EntitySpatialGrid : IDisposable
{
    private static readonly ProfilerMarker RebuildMarker      = new("EntitySpatialGrid.Rebuild");
    private static readonly ProfilerMarker FindNearestMarker  = new("EntitySpatialGrid.FindNearest");
    private static readonly ProfilerMarker FindInRadiusMarker = new("EntitySpatialGrid.FindInRadius");
    private static readonly ProfilerMarker SurveyMarker       = new("EntitySpatialGrid.Survey");

    private NativeArray<int>   _cellEnd;
    private int[]              _bucketOf;
    private NativeArray<int>   _items;
    private NativeArray<int>   _visit;
    private const int NoColony = -1;

    private NativeArray<int>   _ids;
    private NativeArray<int>   _colony;
    private NativeArray<int2>  _cell;
    private NativeArray<byte>  _alive;
    private NativeArray<byte>  _alarm;
    private NativeArray<float> _hp;
    private NativeArray<float> _px;
    private NativeArray<float> _py;
    private NativeArray<float> _pz;
    private EntityModel[]      _models;

    private int   _count;
    private int   _bucketMask;
    private int   _epoch;
    private float _cellSize = 1f;
    private float _invCell  = 1f;

    public int   Count    => _count;
    public float CellSize => _cellSize;
    public bool  IsStale  { get; private set; } = true;
    public bool  IsReady  => _count > 0 && !IsStale && _items.IsCreated;
    public int   BucketMask => _bucketMask;
    public float InvCell    => _invCell;

    public NativeArray<int>   CellEnd => _cellEnd;
    public NativeArray<int>   Items   => _items;
    public NativeArray<int>   Ids     => _ids;
    public NativeArray<int>   Visit   => _visit;
    public NativeArray<byte>  Alive   => _alive;
    public NativeArray<byte>  Alarm   => _alarm;
    public NativeArray<int>   Colony  => _colony;
    public NativeArray<int2>  Cell    => _cell;
    public NativeArray<float> Py      => _py;
    public NativeArray<float> HpFraction => _hp;
    public NativeArray<float> Px      => _px;
    public NativeArray<float> Pz      => _pz;

    public void MarkStale() => IsStale = true;

    public void NoteDeath(EntityModel model)
    {
        int slot = model.GridSlot;
        if ((uint)slot < (uint)_count) _alive[slot] = 0;
    }

    public void NoteHp(EntityModel model, float hp, float maxHealth)
    {
        int slot = model.GridSlot;
        if ((uint)slot < (uint)_count) _hp[slot] = maxHealth > 0f ? math.saturate(hp / maxHealth) : 1f;
    }

    public float HpFractionOf(EntityModel model)
    {
        int slot = model.GridSlot;
        return (uint)slot < (uint)_count ? _hp[slot] : -1f;
    }

    public EntityModel ModelAt(int slot) => (uint)slot < (uint)_count ? _models[slot] : null;

    public EntityModel LiveModelAt(int slot)
        => (uint)slot < (uint)_count && _alive[slot] != 0 ? _models[slot] : null;

    private int SlotOfStats(StatsComponent key)
    {
        if (key == null) return -1;
        for (int i = 0; i < _count; i++)
            if (ReferenceEquals(_models[i].Stats, key)) return i;
        return -1;
    }

    public unsafe int ReserveEpochs(int count)
    {
        if (_epoch > int.MaxValue - count - 2)
        {
            UnsafeUtility.MemClear(_visit.GetUnsafePtr(), (long)_visit.Length * sizeof(int));
            _epoch = 0;
        }
        int start = _epoch;
        _epoch += count;
        return start;
    }

    public unsafe void Rebuild(EntityModel[] snapshot, int count, float cellSize, NativeArray<float3> positions)
    {
        using var scope = RebuildMarker.Auto();
        if (cellSize < 0.01f) cellSize = 0.01f;
        _cellSize = cellSize;
        _invCell  = 1f / cellSize;

        EnsureCapacity(count);

        int buckets = _bucketMask + 1;
        UnsafeUtility.MemClear(_cellEnd.GetUnsafePtr(), (long)(buckets + 1) * sizeof(int));

        int prev    = _count;
        int written = 0;
        for (int i = 0; i < count; i++)
        {
            var e = snapshot[i];
            if (e == null) continue;
            if (e.IsDisposed || e.IsDeathPending || (uint)e.ManagerIndex >= (uint)positions.Length)
            {
                e.GridSlot = -1;
                continue;
            }

            float3 p = positions[e.ManagerIndex];
            _px[written]     = p.x;
            _py[written]     = p.y;
            _pz[written]     = p.z;
            _models[written] = e;
            _ids[written]    = e.ManagerIndex;
            _colony[written] = e.Colony != null ? e.Colony.Index : NoColony;
            _alive[written]  = 1;
            _alarm[written]  = e.StressLevel == CreatureCore.StressHigh ? (byte)1 : (byte)0;
            var stats = e.Stats;
            _hp[written]     = stats != null && !stats.IsDisposed && stats.MaxHealth > 0f ? math.saturate(stats.Hp / stats.MaxHealth) : 1f;
            e.GridSlot       = written;

            int cellX = CellCoord(p.x);
            int cellZ = CellCoord(p.z);
            _cell[written] = new int2(cellX, cellZ);
            int b = BucketOf(cellX, cellZ);
            _bucketOf[written] = b;
            _cellEnd[b + 1]++;
            written++;
        }

        if (prev > written) Array.Clear(_models, written, prev - written);
        _count = written;

        for (int b = 0; b < buckets; b++)
            _cellEnd[b + 1] += _cellEnd[b];

        for (int i = 0; i < written; i++)
            _items[_cellEnd[_bucketOf[i]]++] = i;

        IsStale = false;
    }

    public EntityModel FindNearest(Vector3 origin, EntityModel self, float radius, out float distance)
    {
        using var scope = FindNearestMarker.Auto();
        distance = -1f;
        if (_count == 0) return null;

        float r2   = radius * radius;
        int   ring = RingOf(radius);
        int   cx   = CellCoord(origin.x);
        int   cz   = CellCoord(origin.z);
        int   epoch = NextEpoch();

        int selfSlot = self != null ? self.GridSlot : -1;
        int bestSlot = -1;
        float minSqr = float.MaxValue;

        for (int dz = -ring; dz <= ring; dz++)
        for (int dx = -ring; dx <= ring; dx++)
        {
            int b = BucketOf(cx + dx, cz + dz);
            if (_visit[b] == epoch) continue;
            _visit[b] = epoch;

            int s = b == 0 ? 0 : _cellEnd[b - 1];
            int e = _cellEnd[b];

            for (int k = s; k < e; k++)
            {
                int i = _items[k];

                float ddx = _px[i] - origin.x;
                float ddy = _py[i] - origin.y;
                float ddz = _pz[i] - origin.z;
                float sqr = ddx * ddx + ddy * ddy + ddz * ddz;
                if (sqr > r2 || sqr >= minSqr) continue;
                if (i == selfSlot || _alive[i] == 0) continue;

                minSqr   = sqr;
                bestSlot = i;
            }
        }

        if (bestSlot < 0) return null;
        distance = math.sqrt(minSqr);
        return _models[bestSlot];
    }

    public EntityModel Survey(Vector3 origin, EntityModel self, ColonyState colony,
        float nearestRadius, float crowdRadius, int crowdCap,
        out float nearestDistance, out int crowd, out int kin,
        out EntityModel foreign, out float foreignDistance,
        EntityModel lastVictim = null, float stickMul = 1f, float lowHpMul = 1f,
        StatsComponent familiarKey = null, float familiarMul = 1f)
    {
        using var scope = SurveyMarker.Auto();
        nearestDistance = -1f;
        foreignDistance = -1f;
        foreign = null;
        crowd = 0;
        kin   = 0;
        if (_count == 0) return null;

        int colonyIndex = colony != null ? colony.Index : NoColony;

        float nr2   = nearestRadius * nearestRadius;
        float cr2   = crowdRadius * crowdRadius;
        int   ring  = RingOf(math.max(nearestRadius, crowdRadius));
        int   cx    = CellCoord(origin.x);
        int   cz    = CellCoord(origin.z);
        int   epoch = NextEpoch();

        int selfSlot     = self != null ? self.GridSlot : -1;
        int victimSlot   = lastVictim != null ? lastVictim.GridSlot : -1;
        int familiarSlot = SlotOfStats(familiarKey);
        int bestSlot     = -1;
        int foreignSlot  = -1;
        float minSqr = float.MaxValue;
        float minForeignSqr = float.MaxValue;
        float minForeignScore = float.MaxValue;

        for (int dz = -ring; dz <= ring; dz++)
        for (int dx = -ring; dx <= ring; dx++)
        {
            int b = BucketOf(cx + dx, cz + dz);
            if (_visit[b] == epoch) continue;
            _visit[b] = epoch;

            int s = b == 0 ? 0 : _cellEnd[b - 1];
            int e = _cellEnd[b];

            for (int k = s; k < e; k++)
            {
                int i = _items[k];

                float ddx = _px[i] - origin.x;
                float ddy = _py[i] - origin.y;
                float ddz = _pz[i] - origin.z;
                float sqr = ddx * ddx + ddy * ddy + ddz * ddz;

                bool asNearest = sqr <= nr2 && sqr < minSqr;
                bool asCrowd   = sqr <= cr2 && crowd < crowdCap;
                bool asForeign = sqr <= nr2 && _colony[i] != colonyIndex;
                if (!asNearest && !asCrowd && !asForeign) continue;
                if (i == selfSlot || _alive[i] == 0) continue;

                if (asNearest)
                {
                    minSqr   = sqr;
                    bestSlot = i;
                }
                if (asCrowd)
                {
                    crowd++;
                    if (_colony[i] == colonyIndex) kin++;
                }
                if (asForeign)
                {
                    float score = math.sqrt(sqr) * (i == victimSlot ? stickMul : 1f) * math.lerp(lowHpMul, 1f, _hp[i])
                                * (i == familiarSlot ? familiarMul : 1f);
                    if (score < minForeignScore)
                    {
                        minForeignScore = score;
                        minForeignSqr   = sqr;
                        foreignSlot     = i;
                    }
                }
            }
        }

        if (foreignSlot >= 0)
        {
            foreign = _models[foreignSlot];
            foreignDistance = math.sqrt(minForeignSqr);
        }
        if (bestSlot < 0) return null;
        nearestDistance = math.sqrt(minSqr);
        return _models[bestSlot];
    }

    public int FindInRadius(Vector3 origin, EntityModel self, float radius, EntityModel[] result)
    {
        using var scope = FindInRadiusMarker.Auto();
        int cap = result.Length;
        if (_count == 0 || cap == 0) return 0;

        float r2   = radius * radius;
        int   ring = RingOf(radius);
        int   cx   = CellCoord(origin.x);
        int   cz   = CellCoord(origin.z);
        int   epoch = NextEpoch();
        int   written = 0;
        int   selfSlot = self != null ? self.GridSlot : -1;

        for (int dz = -ring; dz <= ring; dz++)
        for (int dx = -ring; dx <= ring; dx++)
        {
            int b = BucketOf(cx + dx, cz + dz);
            if (_visit[b] == epoch) continue;
            _visit[b] = epoch;

            int s = b == 0 ? 0 : _cellEnd[b - 1];
            int e = _cellEnd[b];

            for (int k = s; k < e; k++)
            {
                int i = _items[k];

                float ddx = _px[i] - origin.x;
                float ddy = _py[i] - origin.y;
                float ddz = _pz[i] - origin.z;
                if (ddx * ddx + ddy * ddy + ddz * ddz > r2) continue;
                if (i == selfSlot || _alive[i] == 0) continue;

                result[written++] = _models[i];
                if (written == cap) return written;
            }
        }
        return written;
    }

    public int CountAlarmed(float3 origin, float radius, out int near)
    {
        near = 0;
        if (_count == 0 || !(radius > 0f)) return 0;

        float r2    = radius * radius;
        int   ring  = RingOf(radius);
        int   cx    = CellCoord(origin.x);
        int   cz    = CellCoord(origin.z);
        int   epoch = NextEpoch();
        int   alarmed = 0;

        for (int dz = -ring; dz <= ring; dz++)
        for (int dx = -ring; dx <= ring; dx++)
        {
            int b = BucketOf(cx + dx, cz + dz);
            if (_visit[b] == epoch) continue;
            _visit[b] = epoch;

            int s = b == 0 ? 0 : _cellEnd[b - 1];
            int e = _cellEnd[b];

            for (int k = s; k < e; k++)
            {
                int i = _items[k];
                if (_alive[i] == 0) continue;
                float ddx = _px[i] - origin.x;
                float ddy = _py[i] - origin.y;
                float ddz = _pz[i] - origin.z;
                if (ddx * ddx + ddy * ddy + ddz * ddz > r2) continue;
                near++;
                if (_alarm[i] != 0) alarmed++;
            }
        }
        return alarmed;
    }

    private int RingOf(float radius) => math.max(1, (int)math.ceil(radius * _invCell));

    private int CellCoord(float v) => (int)math.floor(v * _invCell);

    private int BucketOf(int cx, int cz) => HashCell(cx, cz, _bucketMask);

    public static int HashCell(int cx, int cz, int mask)
        => ((cx * 73856093) ^ (cz * 19349663)) & mask;

    private unsafe int NextEpoch()
    {
        if (++_epoch != int.MaxValue) return _epoch;
        UnsafeUtility.MemClear(_visit.GetUnsafePtr(), (long)_visit.Length * sizeof(int));
        _epoch = 1;
        return _epoch;
    }

    private void EnsureCapacity(int count)
    {
        if (_items.IsCreated && _items.Length >= count) return;

        int cap = 64;
        while (cap < count) cap <<= 1;

        DisposeNative();

        _items    = new NativeArray<int>(cap, Allocator.Persistent);
        _ids      = new NativeArray<int>(cap, Allocator.Persistent);
        _colony   = new NativeArray<int>(cap, Allocator.Persistent);
        _cell     = new NativeArray<int2>(cap, Allocator.Persistent);
        _alive    = new NativeArray<byte>(cap, Allocator.Persistent);
        _alarm    = new NativeArray<byte>(cap, Allocator.Persistent);
        _hp       = new NativeArray<float>(cap, Allocator.Persistent);
        _bucketOf = new int[cap];
        _px       = new NativeArray<float>(cap, Allocator.Persistent);
        _py       = new NativeArray<float>(cap, Allocator.Persistent);
        _pz       = new NativeArray<float>(cap, Allocator.Persistent);
        _models   = new EntityModel[cap];

        int buckets = cap << 1;
        _bucketMask = buckets - 1;
        _cellEnd    = new NativeArray<int>(buckets + 1, Allocator.Persistent);
        _visit      = new NativeArray<int>(buckets, Allocator.Persistent);
        _epoch      = 0;
        _count      = 0;
    }

    private void DisposeNative()
    {
        if (_items.IsCreated)   _items.Dispose();
        if (_ids.IsCreated)     _ids.Dispose();
        if (_colony.IsCreated)  _colony.Dispose();
        if (_cell.IsCreated)    _cell.Dispose();
        if (_alive.IsCreated)   _alive.Dispose();
        if (_alarm.IsCreated)   _alarm.Dispose();
        if (_hp.IsCreated)      _hp.Dispose();
        if (_px.IsCreated)      _px.Dispose();
        if (_py.IsCreated)      _py.Dispose();
        if (_pz.IsCreated)      _pz.Dispose();
        if (_cellEnd.IsCreated) _cellEnd.Dispose();
        if (_visit.IsCreated)   _visit.Dispose();
    }

    public void Dispose()
    {
        DisposeNative();
        _count = 0;
    }
}