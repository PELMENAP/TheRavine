using System;
using Unity.Collections;
using Unity.Mathematics;

public sealed class SkillGraphStore : IDisposable
{
    private readonly int _capacity;
    private readonly int _skillCount;
    private NativeArray<float> _levels;
    private NativeArray<float> _peaks;
    private NativeArray<float> _lastUsed;
    private NativeArray<float> _talent;
    private NativeArray<byte> _root;
    private NativeArray<byte> _dirty;

    public int Capacity => _capacity;
    public int SkillCount => _skillCount;
    public bool IsCreated => _levels.IsCreated;

    public NativeArray<float> Levels => _levels;
    public NativeArray<float> Peaks => _peaks;
    public NativeArray<float> LastUsed => _lastUsed;
    public NativeArray<float> Talents => _talent;
    public NativeArray<byte> Roots => _root;
    public NativeArray<byte> Dirty => _dirty;

    public SkillGraphStore(int capacity, int skillCount)
    {
        _capacity = math.max(capacity, 1);
        _skillCount = skillCount;
        int total = _capacity * skillCount;
        _levels = new NativeArray<float>(total, Allocator.Persistent);
        _peaks = new NativeArray<float>(total, Allocator.Persistent);
        _lastUsed = new NativeArray<float>(total, Allocator.Persistent);
        _talent = new NativeArray<float>(total, Allocator.Persistent);
        _root = new NativeArray<byte>(_capacity, Allocator.Persistent);
        _dirty = new NativeArray<byte>(_capacity, Allocator.Persistent);
    }

    public float Level(int index, int skill) => _levels[index * _skillCount + skill];
    public float Peak(int index, int skill) => _peaks[index * _skillCount + skill];
    public float Talent(int index, int skill) => _talent[index * _skillCount + skill];
    public int Root(int index) => _root[index];
    public bool IsDirty(int index) => _dirty[index] != 0;
    public void ClearDirty(int index) => _dirty[index] = 0;
    public void MarkDirty(int index) => _dirty[index] = 1;

    public void SetLevel(int index, int skill, float level, float now)
    {
        int slot = index * _skillCount + skill;
        _levels[slot] = level;
        _peaks[slot] = math.max(_peaks[slot], level);
        _lastUsed[slot] = now;
        _dirty[index] = 1;
    }

    public void Reset(int index, float2 origin, SkillGraphTopology topology, SkillAtlas atlas)
    {
        if ((uint)index >= (uint)_capacity) return;
        ClearBlock(index);
        Retarget(index, origin, topology, atlas);
        _dirty[index] = 1;
    }

    public void Retarget(int index, float2 origin, SkillGraphTopology topology, SkillAtlas atlas)
    {
        if ((uint)index >= (uint)_capacity) return;
        ref readonly var rules = ref SimulationRules.Frame;
        var positions = atlas.NativePositions;
        int root = SkillGraphMath.RootVertex(origin, topology.IslandMask, in positions);
        _root[index] = (byte)root;
        int baseIndex = index * _skillCount;
        int diameter = topology.TreeDiameter;
        for (int v = 0; v < _skillCount; v++)
            _talent[baseIndex + v] = SkillGraphMath.Talent(origin, positions[v], topology.TreeDistance(root, v), diameter, in rules);
    }

    public bool AddXp(int index, int skill, float xp, float now, SkillGraphTopology topology, SkillAtlas atlas, in SimulationRules.RulesFrame rules)
    {
        if ((uint)index >= (uint)_capacity || (uint)skill >= (uint)_skillCount || !(xp > 0f)) return false;
        int baseIndex = index * _skillCount;
        int root = _root[index];
        var active = topology.ActiveRows;
        if (!SkillGraphMath.IsAvailable(skill, root, topology.ComponentOf(root), topology.PrunedMask, in active, in _peaks, baseIndex))
            return false;

        int slot = baseIndex + skill;
        float level = SkillGraphMath.Decay(_levels[slot], _peaks[slot], now - _lastUsed[slot], topology.CycleFactor(skill), in rules);
        float peak = _peaks[slot];
        var positions = atlas.NativePositions;
        float envDelta = SkillGraphMath.EnvironmentDelta(level, peak, skill, active[skill], in _levels, baseIndex, in positions, atlas.MeanEdgeLength);
        float boost = SkillGraphMath.CliqueBoost(topology.CliqueSize(skill), in rules)
            * SkillGraphMath.LogicBoost(envDelta, level, in rules)
            * SkillGraphMath.FocusBoost(math.countbits(topology.PrunedMask), _skillCount, in rules);

        float before = math.floor(level);
        level += SkillGraphMath.XpGain(xp, level, atlas.NativeXpCost[skill], _talent[slot], boost, in rules);
        _levels[slot] = level;
        _peaks[slot] = math.max(peak, level);
        _lastUsed[slot] = now;
        bool changed = math.floor(level) != before;
        if (changed) _dirty[index] = 1;
        return changed;
    }

    public void Materialize(int index, float now, SkillGraphTopology topology, in SimulationRules.RulesFrame rules)
    {
        if ((uint)index >= (uint)_capacity) return;
        int baseIndex = index * _skillCount;
        ulong pruned = topology.PrunedMask;
        for (int v = 0; v < _skillCount; v++)
        {
            if (((pruned >> v) & 1ul) != 0ul) continue;
            int slot = baseIndex + v;
            float level = SkillGraphMath.Decay(_levels[slot], _peaks[slot], now - _lastUsed[slot], topology.CycleFactor(v), in rules);
            if (math.floor(level) != math.floor(_levels[slot])) _dirty[index] = 1;
            _levels[slot] = level;
            _lastUsed[slot] = now;
        }
    }

    public void Thaw(int index, ulong restored, float now)
    {
        if ((uint)index >= (uint)_capacity) return;
        int baseIndex = index * _skillCount;
        for (ulong rest = restored; rest != 0ul; rest &= rest - 1ul)
            _lastUsed[baseIndex + math.tzcnt(rest)] = now;
        _dirty[index] = 1;
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)_capacity || (uint)last >= (uint)_capacity) return;
        if (index != last)
        {
            int n = _skillCount;
            NativeArray<float>.Copy(_levels, last * n, _levels, index * n, n);
            NativeArray<float>.Copy(_peaks, last * n, _peaks, index * n, n);
            NativeArray<float>.Copy(_lastUsed, last * n, _lastUsed, index * n, n);
            NativeArray<float>.Copy(_talent, last * n, _talent, index * n, n);
            _root[index] = _root[last];
            _dirty[index] = _dirty[last];
        }
        ClearBlock(last);
    }

    private void ClearBlock(int index)
    {
        int baseIndex = index * _skillCount;
        for (int v = 0; v < _skillCount; v++)
        {
            _levels[baseIndex + v] = 0f;
            _peaks[baseIndex + v] = 0f;
            _lastUsed[baseIndex + v] = 0f;
            _talent[baseIndex + v] = 0f;
        }
        _root[index] = 0;
        _dirty[index] = 0;
    }

    public void Dispose()
    {
        if (_levels.IsCreated) _levels.Dispose();
        if (_peaks.IsCreated) _peaks.Dispose();
        if (_lastUsed.IsCreated) _lastUsed.Dispose();
        if (_talent.IsCreated) _talent.Dispose();
        if (_root.IsCreated) _root.Dispose();
        if (_dirty.IsCreated) _dirty.Dispose();
    }
}
