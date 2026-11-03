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
    private NativeArray<int>   _ids;
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
    public NativeArray<float> Px      => _px;
    public NativeArray<float> Pz      => _pz;

    public void MarkStale() => IsStale = true;

    public int ReserveEpochs(int count)
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

    public void Rebuild(EntityModel[] snapshot, int count, float cellSize)
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
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;

            Vector3 p = e.Motor.Position();
            _px[written]     = p.x;
            _py[written]     = p.y;
            _pz[written]     = p.z;
            _models[written] = e;
            _ids[written]    = e.ManagerIndex;

            int b = BucketOf(CellCoord(p.x), CellCoord(p.z));
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

        EntityModel best = null;
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

                var m = _models[i];
                if (ReferenceEquals(m, self) || m.IsDeathPending || m.IsDisposed) continue;

                minSqr = sqr;
                best   = m;
            }
        }

        if (best != null) distance = math.sqrt(minSqr);
        return best;
    }

    public EntityModel Survey(Vector3 origin, EntityModel self, ColonyState colony,
        float nearestRadius, float crowdRadius, int crowdCap,
        out float nearestDistance, out int crowd, out int kin)
    {
        using var scope = SurveyMarker.Auto();
        nearestDistance = -1f;
        crowd = 0;
        kin   = 0;
        if (_count == 0) return null;

        float nr2   = nearestRadius * nearestRadius;
        float cr2   = crowdRadius * crowdRadius;
        int   ring  = RingOf(math.max(nearestRadius, crowdRadius));
        int   cx    = CellCoord(origin.x);
        int   cz    = CellCoord(origin.z);
        int   epoch = NextEpoch();

        EntityModel best = null;
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

                bool asNearest = sqr <= nr2 && sqr < minSqr;
                bool asCrowd   = sqr <= cr2 && crowd < crowdCap;
                if (!asNearest && !asCrowd) continue;

                var m = _models[i];
                if (ReferenceEquals(m, self) || m.IsDeathPending || m.IsDisposed) continue;

                if (asNearest)
                {
                    minSqr = sqr;
                    best   = m;
                }
                if (asCrowd)
                {
                    crowd++;
                    if (m.Colony == colony) kin++;
                }
            }
        }

        if (best != null) nearestDistance = math.sqrt(minSqr);
        return best;
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

                var m = _models[i];
                if (ReferenceEquals(m, self) || m.IsDeathPending || m.IsDisposed) continue;

                result[written++] = m;
                if (written == cap) return written;
            }
        }
        return written;
    }

    private int RingOf(float radius) => math.max(1, (int)math.ceil(radius * _invCell));

    private int CellCoord(float v) => (int)math.floor(v * _invCell);

    private int BucketOf(int cx, int cz) => HashCell(cx, cz, _bucketMask);

    public static int HashCell(int cx, int cz, int mask)
        => ((cx * 73856093) ^ (cz * 19349663)) & mask;

    private int NextEpoch()
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