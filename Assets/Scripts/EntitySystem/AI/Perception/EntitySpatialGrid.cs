using System;
using Unity.Mathematics;
using UnityEngine;

public sealed class EntitySpatialGrid
{
    private int[]         _cellEnd;
    private int[]         _bucketOf;
    private int[]         _items;
    private int[]         _visit;
    private float[]       _px;
    private float[]       _py;
    private float[]       _pz;
    private EntityModel[] _models;

    private int   _count;
    private int   _bucketMask;
    private int   _epoch;
    private float _cellSize = 1f;
    private float _invCell  = 1f;

    public int   Count    => _count;
    public float CellSize => _cellSize;

    public void Rebuild(EntityModel[] snapshot, int count, float cellSize)
    {
        if (cellSize < 0.01f) cellSize = 0.01f;
        _cellSize = cellSize;
        _invCell  = 1f / cellSize;

        EnsureCapacity(count);

        int buckets = _bucketMask + 1;
        Array.Clear(_cellEnd, 0, buckets + 1);

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
    }

    public EntityModel FindNearest(Vector3 origin, EntityModel self, float radius, out float distance)
    {
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

    public int FindInRadius(Vector3 origin, EntityModel self, float radius, EntityModel[] result)
    {
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

    private int BucketOf(int cx, int cz)
        => ((cx * 73856093) ^ (cz * 19349663)) & _bucketMask;

    private int NextEpoch()
    {
        if (++_epoch != int.MaxValue) return _epoch;
        Array.Clear(_visit, 0, _visit.Length);
        _epoch = 1;
        return _epoch;
    }

    private void EnsureCapacity(int count)
    {
        if (_items != null && _items.Length >= count) return;

        int cap = 64;
        while (cap < count) cap <<= 1;

        _items    = new int[cap];
        _bucketOf = new int[cap];
        _px       = new float[cap];
        _py       = new float[cap];
        _pz       = new float[cap];
        _models   = new EntityModel[cap];

        int buckets = cap << 1;
        _bucketMask = buckets - 1;
        _cellEnd    = new int[buckets + 1];
        _visit      = new int[buckets];
        _epoch      = 0;
        _count      = 0;
    }
}