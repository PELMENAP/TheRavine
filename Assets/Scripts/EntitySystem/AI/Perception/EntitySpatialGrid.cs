using UnityEngine;

public sealed class EntitySpatialGrid
{
    private int[]        _cellEnd;
    private int[]        _bucketOf;
    private int[]        _items;
    private float[]      _px;
    private float[]      _py;
    private float[]      _pz;
    private GameObject[] _gos;

    private int   _count;
    private int   _bucketMask;
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
        System.Array.Clear(_cellEnd, 0, buckets + 1);

        int written = 0;
        for (int i = 0; i < count; i++)
        {
            var e = snapshot[i];
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;

            Vector3 p = e.Motor.Position();
            _px[written]  = p.x;
            _py[written]  = p.y;
            _pz[written]  = p.z;
            _gos[written] = e.SelfObject;

            int b = BucketOf(CellCoord(p.x), CellCoord(p.z));
            _bucketOf[written] = b;
            _cellEnd[b + 1]++;
            written++;
        }

        _count = written;

        for (int b = 0; b < buckets; b++)
            _cellEnd[b + 1] += _cellEnd[b];

        for (int i = 0; i < written; i++)
            _items[_cellEnd[_bucketOf[i]]++] = i;
    }

    public GameObject FindNearest(Vector3 origin, GameObject self, float radius, out float distance)
    {
        distance = -1f;
        if (_count == 0) return null;

        float r2 = radius * radius;
        int   ring = Mathf.Max(1, Mathf.CeilToInt(radius * _invCell));

        int cx = CellCoord(origin.x);
        int cz = CellCoord(origin.z);

        GameObject best = null;
        float minSqr = float.MaxValue;

        for (int dz = -ring; dz <= ring; dz++)
        for (int dx = -ring; dx <= ring; dx++)
        {
            int b = BucketOf(cx + dx, cz + dz);
            int s = b == 0 ? 0 : _cellEnd[b - 1];
            int e = _cellEnd[b];

            for (int k = s; k < e; k++)
            {
                int i = _items[k];
                var go = _gos[i];
                if (go == null || go == self) continue;

                float ddx = _px[i] - origin.x;
                float ddy = _py[i] - origin.y;
                float ddz = _pz[i] - origin.z;
                float sqr = ddx * ddx + ddy * ddy + ddz * ddz;

                if (sqr > r2 || sqr >= minSqr) continue;
                minSqr = sqr;
                best = go;
            }
        }

        if (best != null) distance = Mathf.Sqrt(minSqr);
        return best;
    }

    public int FindInRadius(Vector3 origin, GameObject self, float radius, GameObject[] result)
    {
        int written = 0;
        for (int i = 0; i < result.Length; i++) result[i] = null;
        if (_count == 0) return 0;

        float r2 = radius * radius;
        int   ring = Mathf.Max(1, Mathf.CeilToInt(radius * _invCell));

        int cx = CellCoord(origin.x);
        int cz = CellCoord(origin.z);

        for (int dz = -ring; dz <= ring && written < result.Length; dz++)
        for (int dx = -ring; dx <= ring && written < result.Length; dx++)
        {
            int b = BucketOf(cx + dx, cz + dz);
            int s = b == 0 ? 0 : _cellEnd[b - 1];
            int e = _cellEnd[b];

            for (int k = s; k < e && written < result.Length; k++)
            {
                int i = _items[k];
                var go = _gos[i];
                if (go == null || go == self) continue;

                float ddx = _px[i] - origin.x;
                float ddy = _py[i] - origin.y;
                float ddz = _pz[i] - origin.z;
                if (ddx * ddx + ddy * ddy + ddz * ddz > r2) continue;

                result[written++] = go;
            }
        }
        return written;
    }

    private int CellCoord(float v) => Mathf.FloorToInt(v * _invCell);

    private int BucketOf(int cx, int cz)
        => ((cx * 73856093) ^ (cz * 19349663)) & _bucketMask;

    private void EnsureCapacity(int count)
    {
        if (_items == null || _items.Length < count)
        {
            int cap = 64;
            while (cap < count) cap <<= 1;

            _items    = new int[cap];
            _bucketOf = new int[cap];
            _px       = new float[cap];
            _py       = new float[cap];
            _pz       = new float[cap];
            _gos      = new GameObject[cap];

            int buckets = cap << 1;
            _bucketMask = buckets - 1;
            _cellEnd    = new int[buckets + 1];
        }
    }
}