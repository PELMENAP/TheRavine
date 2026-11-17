using Unity.Collections;
using Unity.Mathematics;

public sealed partial class SkillGraphTopology
{
    private NativeArray<int> _disc;
    private NativeArray<int> _low;
    private NativeArray<int> _parent;
    private NativeArray<int> _stackVertex;
    private NativeArray<ulong> _stackRemaining;
    private NativeArray<ushort> _edgeStack;
    private NativeArray<float> _blockScore;

    private void AllocateScratch()
    {
        _disc = new NativeArray<int>(_count, Allocator.Persistent);
        _low = new NativeArray<int>(_count, Allocator.Persistent);
        _parent = new NativeArray<int>(_count, Allocator.Persistent);
        _stackVertex = new NativeArray<int>(_count, Allocator.Persistent);
        _stackRemaining = new NativeArray<ulong>(_count, Allocator.Persistent);
        _edgeStack = new NativeArray<ushort>(math.max(_count * (_count - 1) / 2, 1), Allocator.Persistent);
        _blockScore = new NativeArray<float>(_count, Allocator.Persistent);
    }

    private void DisposeScratch()
    {
        if (_disc.IsCreated) _disc.Dispose();
        if (_low.IsCreated) _low.Dispose();
        if (_parent.IsCreated) _parent.Dispose();
        if (_stackVertex.IsCreated) _stackVertex.Dispose();
        if (_stackRemaining.IsCreated) _stackRemaining.Dispose();
        if (_edgeStack.IsCreated) _edgeStack.Dispose();
        if (_blockScore.IsCreated) _blockScore.Dispose();
    }

    private void Rebuild()
    {
        ref readonly var rules = ref SimulationRules.Frame;
        int n = _count;
        for (int v = 0; v < n; v++)
        {
            _disc[v] = -1;
            _low[v] = 0;
            _parent[v] = -1;
            _bridges[v] = 0ul;
            _blockScore[v] = 0f;
            _componentOf[v] = 0ul;
        }

        int time = 0;
        for (int root = 0; root < n; root++)
        {
            if (_disc[root] >= 0) continue;
            ulong component = 1ul << root;
            int sp = 0, esp = 0;
            _stackVertex[0] = root;
            _stackRemaining[0] = _active[root];
            _disc[root] = _low[root] = time++;

            while (sp >= 0)
            {
                int u = _stackVertex[sp];
                ulong remaining = _stackRemaining[sp];
                if (remaining != 0ul)
                {
                    int v = math.tzcnt(remaining);
                    _stackRemaining[sp] = remaining & (remaining - 1ul);
                    if (v == _parent[u]) continue;
                    if (_disc[v] < 0)
                    {
                        _parent[v] = u;
                        _disc[v] = _low[v] = time++;
                        component |= 1ul << v;
                        _edgeStack[esp++] = (ushort)((u << 8) | v);
                        sp++;
                        _stackVertex[sp] = v;
                        _stackRemaining[sp] = _active[v];
                    }
                    else if (_disc[v] < _disc[u])
                    {
                        _edgeStack[esp++] = (ushort)((u << 8) | v);
                        _low[u] = math.min(_low[u], _disc[v]);
                    }
                    continue;
                }

                sp--;
                if (sp < 0) break;
                int p = _stackVertex[sp];
                _low[p] = math.min(_low[p], _low[u]);
                if (_low[u] < _disc[p]) continue;

                int edges = 0;
                ulong members = 0ul;
                while (esp > 0)
                {
                    ushort e = _edgeStack[--esp];
                    int a = e >> 8, b = e & 255;
                    members |= (1ul << a) | (1ul << b);
                    edges++;
                    if (a == p && b == u) break;
                }
                int size = math.countbits(members);
                if (edges == 1)
                {
                    _bridges[p] |= 1ul << u;
                    _bridges[u] |= 1ul << p;
                }
                float score = SkillGraphMath.BlockScore(edges, size);
                for (ulong rest = members; rest != 0ul; rest &= rest - 1ul)
                {
                    int w = math.tzcnt(rest);
                    _blockScore[w] = math.max(_blockScore[w], score);
                }
            }

            for (ulong rest = component; rest != 0ul; rest &= rest - 1ul) _componentOf[math.tzcnt(rest)] = component;
        }

        for (int v = 0; v < n; v++)
        {
            _cycleFactor[v] = SkillGraphMath.CycleFactor(_blockScore[v], in rules);
            _cliqueSize[v] = (byte)CliqueSizeOf(v);
        }
    }

    private int CliqueSizeOf(int v)
    {
        ulong near = _active[v];
        if (near == 0ul) return 1;
        int best = 2;
        for (ulong a = near; a != 0ul; a &= a - 1ul)
        {
            int u = math.tzcnt(a);
            ulong common = near & _active[u];
            if (common == 0ul) continue;
            best = math.max(best, 3);
            for (ulong b = common; b != 0ul; b &= b - 1ul)
            {
                int w = math.tzcnt(b);
                if ((common & _active[w]) != 0ul) return 4;
            }
        }
        return best;
    }
}
