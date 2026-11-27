using System.Collections.Generic;
using Unity.Mathematics;

public struct LineageRecord
{
    public int ParentA;
    public int ParentB;
    public float BirthTime;
    public float DeathTime;
    public int Generation;
    public uint MateLoci;

    public readonly bool Alive => DeathTime < 0f;
}

public sealed class LineageStore
{
    public const int MaxDepth = 6;
    private const int MaxAncestors = (2 << MaxDepth) - 1;

    private readonly Dictionary<int, LineageRecord> _records;
    private readonly Queue<int> _order;
    private readonly int[] _idsA = new int[MaxAncestors];
    private readonly int[] _depthA = new int[MaxAncestors];
    private readonly int[] _idsB = new int[MaxAncestors];
    private readonly int[] _depthB = new int[MaxAncestors];
    private readonly int[] _common = new int[MaxAncestors];
    private readonly int[] _commonDepth = new int[MaxAncestors];
    private readonly int[] _scratch = new int[MaxAncestors];
    private readonly int[] _scratchDepth = new int[MaxAncestors];
    private int _capacity;

    public int Count => _records.Count;
    public int Capacity => _capacity;

    public LineageStore(int capacity)
    {
        _capacity = math.max(capacity, 16);
        _records  = new Dictionary<int, LineageRecord>(_capacity + 1);
        _order    = new Queue<int>(_capacity + 1);
    }

    public void SetCapacity(int capacity) => _capacity = math.max(capacity, 16);

    public bool TryGet(int id, out LineageRecord record) => _records.TryGetValue(id, out record);

    public void Birth(int id, int parentA, int parentB, float time, uint loci)
    {
        if (id <= 0 || _records.ContainsKey(id)) return;
        if (parentB == parentA) parentB = 0;
        int generation = 0;
        if (parentA > 0 && _records.TryGetValue(parentA, out var a)) generation = a.Generation + 1;
        if (parentB > 0 && _records.TryGetValue(parentB, out var b)) generation = math.max(generation, b.Generation + 1);
        _records[id] = new LineageRecord
        {
            ParentA = parentA, ParentB = parentB, BirthTime = time, DeathTime = -1f, Generation = generation, MateLoci = loci,
        };
        _order.Enqueue(id);
        Prune();
    }

    public void Death(int id, float time)
    {
        if (!_records.TryGetValue(id, out var r) || !r.Alive) return;
        r.DeathTime = math.max(time, r.BirthTime);
        _records[id] = r;
    }

    private void Prune()
    {
        int attempts = _order.Count;
        while (_records.Count > _capacity && attempts-- > 0)
        {
            int id = _order.Dequeue();
            if (_records.TryGetValue(id, out var r) && r.Alive) _order.Enqueue(id);
            else _records.Remove(id);
        }
    }

    public float Relatedness(int a, int b, int depth)
    {
        if (a <= 0 || b <= 0) return 0f;
        if (a == b) return 1f;
        depth = math.clamp(depth, 0, MaxDepth);
        int na = Collect(a, depth, _idsA, _depthA);
        int nb = Collect(b, depth, _idsB, _depthB);

        int nc = 0;
        for (int i = 0; i < na; i++)
        {
            int k = IndexOf(_idsB, nb, _idsA[i]);
            if (k < 0) continue;
            _common[nc] = _idsA[i];
            _commonDepth[nc] = _depthA[i] + _depthB[k];
            nc++;
        }

        float r = 0f;
        for (int i = 0; i < nc; i++)
        {
            if (HasCommonDescendant(_common[i], nc)) continue;
            r += math.pow(0.5f, _commonDepth[i]);
        }
        return math.saturate(r);
    }

    private bool HasCommonDescendant(int ancestor, int nc)
    {
        for (int j = 0; j < nc; j++)
        {
            int other = _common[j];
            if (other == ancestor) continue;
            int n = Collect(other, MaxDepth, _scratch, _scratchDepth);
            if (IndexOf(_scratch, n, ancestor) > 0) return true;
        }
        return false;
    }

    private int Collect(int id, int depth, int[] ids, int[] depths)
    {
        int n = 0;
        ids[n] = id;
        depths[n] = 0;
        n++;
        for (int head = 0; head < n; head++)
        {
            if (depths[head] >= depth || !_records.TryGetValue(ids[head], out var r)) continue;
            n = Add(ids, depths, n, r.ParentA, depths[head] + 1);
            n = Add(ids, depths, n, r.ParentB, depths[head] + 1);
        }
        return n;
    }

    private static int Add(int[] ids, int[] depths, int n, int id, int depth)
    {
        if (id <= 0 || n >= ids.Length) return n;
        int k = IndexOf(ids, n, id);
        if (k >= 0)
        {
            if (depth < depths[k]) depths[k] = depth;
            return n;
        }
        ids[n] = id;
        depths[n] = depth;
        return n + 1;
    }

    private static int IndexOf(int[] ids, int n, int id)
    {
        for (int i = 0; i < n; i++)
            if (ids[i] == id) return i;
        return -1;
    }
}
