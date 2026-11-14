using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

public sealed partial class SkillGraphTopology : IDisposable
{
    public const int MaxPrunedBridges = 15;
    public const int UnreachableDistance = 255;
    public const int BuildCost = 1;
    public const int RestoreCost = 2;

    private readonly SkillAtlas _atlas;
    private readonly int _count;
    private NativeArray<ulong> _active;
    private NativeArray<ulong> _bridges;
    private NativeArray<byte> _cliqueSize;
    private NativeArray<float> _cycleFactor;
    private NativeArray<ulong> _componentOf;
    private NativeArray<byte> _treeDistance;
    private int _treeDiameter;

    private ulong _islandMask;
    private ulong _prunedMask;
    private FixedList32Bytes<ushort> _prunedBridges;
    private float _phiPeak;
    private byte _spentEdgePoints;
    private float4 _forestGenes;
    private int _revision;

    public int Count => _count;
    public bool IsCreated => _active.IsCreated;
    public ulong IslandMask => _islandMask;
    public ulong PrunedMask => _prunedMask;
    public int PrunedBridgeCount => _prunedBridges.Length;
    public float PhiPeak => _phiPeak;
    public int SpentEdgePoints => _spentEdgePoints;
    public float4 ForestGenes => _forestGenes;
    public int Revision => _revision;
    public int TreeDiameter => _treeDiameter;
    public SkillAtlas Atlas => _atlas;

    public NativeArray<ulong> ActiveRows => _active;
    public NativeArray<byte> CliqueSizes => _cliqueSize;
    public NativeArray<float> CycleFactors => _cycleFactor;
    public NativeArray<ulong> Components => _componentOf;

    private SkillGraphTopology(SkillAtlas atlas, float4 forestGenes)
    {
        _atlas = atlas;
        _count = atlas.Count;
        _forestGenes = forestGenes;
        _active = new NativeArray<ulong>(_count, Allocator.Persistent);
        _bridges = new NativeArray<ulong>(_count, Allocator.Persistent);
        _cliqueSize = new NativeArray<byte>(_count, Allocator.Persistent);
        _cycleFactor = new NativeArray<float>(_count, Allocator.Persistent);
        _componentOf = new NativeArray<ulong>(_count, Allocator.Persistent);
        _treeDistance = new NativeArray<byte>(_count * _count, Allocator.Persistent);
        AllocateScratch();
    }

    public static float4 DrawForestGenes()
    {
        float range = SimulationRules.IsBound ? SimulationRules.Active.SkillForestGeneRange : 0f;
        return new float4(
            RavineRandom.RangeFloat(-range, range), RavineRandom.RangeFloat(-range, range),
            RavineRandom.RangeFloat(-range, range), RavineRandom.RangeFloat(-range, range));
    }

    public static SkillGraphTopology Create(SkillAtlas atlas, float4 forestGenes)
    {
        var topology = new SkillGraphTopology(atlas, forestGenes);
        topology.BuildForest();
        topology.ComputeTreeDistances();
        topology.BuildIslands();
        topology.Rebuild();
        return topology;
    }

    public ulong ActiveRow(int v) => _active[v];
    public ulong ComponentOf(int v) => _componentOf[v];
    public int CliqueSize(int v) => _cliqueSize[v];
    public float CycleFactor(int v) => _cycleFactor[v];
    public int TreeDistance(int a, int b) => _treeDistance[a * _count + b];

    public bool IsActive(int u, int v)
        => (uint)u < (uint)_count && (uint)v < (uint)_count && ((_active[u] >> v) & 1ul) != 0ul;

    public bool IsBridge(int u, int v)
        => (uint)u < (uint)_count && (uint)v < (uint)_count && ((_bridges[u] >> v) & 1ul) != 0ul;

    public bool IsPrunedBridge(int u, int v)
    {
        for (int i = 0; i < _prunedBridges.Length; i++)
        {
            int a = _prunedBridges[i] >> 8, b = _prunedBridges[i] & 255;
            if ((a == u && b == v) || (a == v && b == u)) return true;
        }
        return false;
    }

    public ushort PrunedBridgeAt(int index) => _prunedBridges[index];

    public ulong MainComponent(int root)
    {
        if (root >= 0 && root < _count) return _componentOf[root];
        ulong best = 0ul;
        int bestSize = -1;
        for (int v = 0; v < _count; v++)
        {
            int size = math.countbits(_componentOf[v]);
            if (size > bestSize) { bestSize = size; best = _componentOf[v]; }
        }
        return best;
    }

    public int Earned(in SimulationRules.RulesFrame f) => SkillGraphMath.Earned(_phiPeak, in f);

    public int Available(in SimulationRules.RulesFrame f) => Earned(in f) - _spentEdgePoints;

    public bool UpdatePhi(float phi)
    {
        if (!(phi > _phiPeak)) return false;
        _phiPeak = phi;
        return true;
    }

    public bool TryBuild(int u, int v)
    {
        if (!CanBuild(u, v)) return false;
        _active[u] |= 1ul << v;
        _active[v] |= 1ul << u;
        _spentEdgePoints = (byte)(_spentEdgePoints + BuildCost);
        Rebuild();
        _revision++;
        return true;
    }

    public bool CanBuild(int u, int v)
    {
        if (!_atlas.HasPotentialEdge(u, v) || IsActive(u, v)) return false;
        if (((_prunedMask >> u) & 1ul) != 0ul || ((_prunedMask >> v) & 1ul) != 0ul) return false;
        return Available(in SimulationRules.Frame) >= BuildCost && _spentEdgePoints + BuildCost <= byte.MaxValue;
    }

    public bool TryPrune(int u, int v, int rootVertex)
    {
        if (!CanPrune(u, v, rootVertex, out ulong far, out int near, out int farVertex)) return false;
        _active[u] &= ~(1ul << v);
        _active[v] &= ~(1ul << u);
        _prunedMask |= far;
        _prunedBridges.Add((ushort)((near << 8) | farVertex));
        Rebuild();
        _revision++;
        return true;
    }

    public bool CanPrune(int u, int v, int rootVertex)
        => CanPrune(u, v, rootVertex, out _, out _, out _);

    private bool CanPrune(int u, int v, int rootVertex, out ulong far, out int near, out int farVertex)
    {
        far = 0ul;
        near = u;
        farVertex = v;
        if ((uint)u >= (uint)_count || (uint)v >= (uint)_count || u == v) return false;
        if (!IsBridge(u, v) || _prunedBridges.Length >= MaxPrunedBridges) return false;
        ulong main = MainComponent(rootVertex);
        if (((main >> u) & 1ul) == 0ul) return false;
        ulong sideU = FloodExcluding(u, u, v);
        ulong sideV = FloodExcluding(v, u, v);
        bool farIsV;
        if (rootVertex >= 0 && rootVertex < _count) farIsV = ((sideU >> rootVertex) & 1ul) != 0ul;
        else farIsV = math.countbits(sideV) <= math.countbits(sideU);
        far = farIsV ? sideV : sideU;
        near = farIsV ? u : v;
        farVertex = farIsV ? v : u;
        if ((far & _islandMask) != 0ul) return false;
        if (rootVertex >= 0 && rootVertex < _count && ((far >> rootVertex) & 1ul) != 0ul) return false;
        return true;
    }

    public bool TryRestore(int u, int v)
    {
        if (!CanRestore(u, v, out int slot, out int near, out int farVertex)) return false;
        ulong far = FloodExcluding(farVertex, near, farVertex);
        _prunedMask &= ~far;
        _active[near] |= 1ul << farVertex;
        _active[farVertex] |= 1ul << near;
        _prunedBridges.RemoveAtSwapBack(slot);
        _spentEdgePoints = (byte)(_spentEdgePoints + RestoreCost);
        Rebuild();
        _revision++;
        return true;
    }

    public bool CanRestore(int u, int v)
        => CanRestore(u, v, out _, out _, out _);

    private bool CanRestore(int u, int v, out int slot, out int near, out int farVertex)
    {
        slot = -1;
        near = u;
        farVertex = v;
        if ((uint)u >= (uint)_count || (uint)v >= (uint)_count || u == v) return false;
        for (int i = 0; i < _prunedBridges.Length; i++)
        {
            int a = _prunedBridges[i] >> 8, b = _prunedBridges[i] & 255;
            if ((a == u && b == v) || (a == v && b == u)) { slot = i; near = a; farVertex = b; break; }
        }
        if (slot < 0) return false;
        if (((_prunedMask >> near) & 1ul) != 0ul) return false;
        return Available(in SimulationRules.Frame) >= RestoreCost && _spentEdgePoints + RestoreCost <= byte.MaxValue;
    }

    private ulong FloodExcluding(int start, int exU, int exV)
    {
        ulong seen = 1ul << start, frontier = seen;
        while (frontier != 0ul)
        {
            int v = math.tzcnt(frontier);
            frontier &= frontier - 1ul;
            ulong row = _active[v];
            if (v == exU) row &= ~(1ul << exV);
            else if (v == exV) row &= ~(1ul << exU);
            ulong fresh = row & ~seen;
            seen |= fresh;
            frontier |= fresh;
        }
        return seen;
    }

    private void BuildForest()
    {
        int n = _count;
        var eu = new List<int>();
        var ev = new List<int>();
        var ew = new List<float>();
        for (int u = 0; u < n; u++)
        for (int v = u + 1; v < n; v++)
        {
            if (!_atlas.HasPotentialEdge(u, v)) continue;
            eu.Add(u);
            ev.Add(v);
            ew.Add(EdgeWeight(u, v));
        }
        var keys = ew.ToArray();
        var order = new int[keys.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(keys, order);

        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        for (int k = 0; k < order.Length; k++)
        {
            int u = eu[order[k]], v = ev[order[k]];
            int ru = Find(parent, u), rv = Find(parent, v);
            if (ru == rv) continue;
            parent[ru] = rv;
            _active[u] |= 1ul << v;
            _active[v] |= 1ul << u;
        }
    }

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    public float EdgeWeight(int u, int v)
    {
        float2 xu = _atlas.PositionOf(u), xv = _atlas.PositionOf(v);
        float2 m = 0.5f * (xu + xv);
        float bias = 1f
            + _forestGenes.x * math.exp(-2f * math.lengthsq(m - new float2(0f, 0f)))
            + _forestGenes.y * math.exp(-2f * math.lengthsq(m - new float2(1f, 0f)))
            + _forestGenes.z * math.exp(-2f * math.lengthsq(m - new float2(0f, 1f)))
            + _forestGenes.w * math.exp(-2f * math.lengthsq(m - new float2(1f, 1f)));
        return math.distance(xu, xv) * math.max(bias, 0f);
    }

    private void ComputeTreeDistances()
    {
        int n = _count;
        _treeDiameter = 0;
        for (int s = 0; s < n; s++)
        {
            for (int t = 0; t < n; t++) _treeDistance[s * n + t] = UnreachableDistance;
            _treeDistance[s * n + s] = 0;
            ulong seen = 1ul << s, layer = seen;
            int depth = 0;
            while (layer != 0ul)
            {
                ulong next = 0ul;
                for (ulong rest = layer; rest != 0ul; rest &= rest - 1ul) next |= _active[math.tzcnt(rest)];
                next &= ~seen;
                depth++;
                for (ulong rest = next; rest != 0ul; rest &= rest - 1ul)
                {
                    _treeDistance[s * n + math.tzcnt(rest)] = (byte)math.min(depth, UnreachableDistance - 1);
                    _treeDiameter = math.max(_treeDiameter, depth);
                }
                seen |= next;
                layer = next;
            }
        }
    }

    private void BuildIslands()
    {
        int n = _count;
        int sizeMax = math.max(3, n / 6);
        var weights = new List<float>();
        var sides = new List<ulong>();
        var us = new List<int>();
        var vs = new List<int>();
        for (int u = 0; u < n; u++)
        for (ulong rest = _active[u] & ~((2ul << u) - 1ul); rest != 0ul; rest &= rest - 1ul)
        {
            int v = math.tzcnt(rest);
            ulong sideU = FloodExcluding(u, u, v), sideV = FloodExcluding(v, u, v);
            ulong small = math.countbits(sideU) <= math.countbits(sideV) ? sideU : sideV;
            int size = math.countbits(small);
            if (size < 2 || size > sizeMax) continue;
            weights.Add(EdgeWeight(u, v));
            sides.Add(small);
            us.Add(u);
            vs.Add(v);
        }

        int limit = SimulationRules.IsBound ? SimulationRules.Active.SkillIslandDropMax : SimulationRules.DefaultSkillIslandDropMax;
        float sum = _forestGenes.x + _forestGenes.y + _forestGenes.z + _forestGenes.w;
        int drop = math.min(1 + (sum > 0f ? 1 : 0), math.max(limit, 0));
        var keys = weights.ToArray();
        var order = new int[keys.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(keys, order);

        int taken = 0;
        for (int k = order.Length - 1; k >= 0 && taken < drop; k--)
        {
            int i = order[k];
            if ((sides[i] & _islandMask) != 0ul) continue;
            _islandMask |= sides[i];
            _active[us[i]] &= ~(1ul << vs[i]);
            _active[vs[i]] &= ~(1ul << us[i]);
            taken++;
        }
    }

    public void Dispose()
    {
        if (_active.IsCreated) _active.Dispose();
        if (_bridges.IsCreated) _bridges.Dispose();
        if (_cliqueSize.IsCreated) _cliqueSize.Dispose();
        if (_cycleFactor.IsCreated) _cycleFactor.Dispose();
        if (_componentOf.IsCreated) _componentOf.Dispose();
        if (_treeDistance.IsCreated) _treeDistance.Dispose();
        DisposeScratch();
    }
}
