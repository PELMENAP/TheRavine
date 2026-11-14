using System;
using Unity.Mathematics;
using Unity.Profiling;

public sealed class SkillGraphSolver
{
    private static readonly ProfilerMarker StepMarker = new("SkillGraphSolver.Step");

    private readonly float[] _meanLevel = new float[SkillAtlas.MaxCount];
    private readonly float[] _meanPeak = new float[SkillAtlas.MaxCount];
    private readonly float[] _meanTalent = new float[SkillAtlas.MaxCount];
    private readonly float[] _need = new float[(int)SkillNeed.Count];
    private float4 _weights;
    private ulong _openMask;
    private ulong _rootMask;

    public int Step(ColonyState colony, SkillGraphStore store, SkillAtlas atlas, SimulationRules rules)
    {
        using var scope = StepMarker.Auto();
        var topology = colony.SkillTopology;
        if (topology == null || !topology.IsCreated || topology.Count != atlas.Count || store.SkillCount != atlas.Count) return 0;
        var members = colony.Members;
        if (members.Length == 0) return 0;

        ref readonly var f = ref SimulationRules.Frame;
        Accumulate(store, topology, members);
        topology.UpdatePhi(PhiOfMeans(atlas.Count, in f));
        FillNeeds(colony, in f);

        int changes = 0;
        while (topology.Available(in f) > 0 && TryPickBuild(topology, atlas, rules, in f, out int u, out int v))
        {
            MaterializeMembers(store, topology, members, in f);
            if (!topology.TryBuild(u, v)) break;
            Commit(colony, store, topology, members);
            changes++;
            Accumulate(store, topology, members);
        }

        if (colony.Pressure >= rules.PressureMigrateMin && TryPickPrune(topology, atlas.Count, rules, out int pu, out int pv))
        {
            MaterializeMembers(store, topology, members, in f);
            if (topology.TryPrune(pu, pv, -1))
            {
                Commit(colony, store, topology, members);
                changes++;
            }
        }
        return changes;
    }

    private void Accumulate(SkillGraphStore store, SkillGraphTopology topology, ReadOnlySpan<int> members)
    {
        int n = topology.Count;
        Array.Clear(_meanLevel, 0, n);
        Array.Clear(_meanPeak, 0, n);
        Array.Clear(_meanTalent, 0, n);
        var levels = store.Levels;
        var peaks = store.Peaks;
        var talents = store.Talents;
        var plast = store.Plasticity;
        var roots = store.Roots;
        float4 plastSum = default;
        _rootMask = 0ul;
        for (int i = 0; i < members.Length; i++)
        {
            int index = members[i];
            int baseIndex = index * n;
            for (int v = 0; v < n; v++)
            {
                _meanLevel[v] += levels[baseIndex + v];
                _meanPeak[v] += peaks[baseIndex + v];
                _meanTalent[v] += talents[baseIndex + v];
            }
            plastSum += plast[index];
            _rootMask |= 1ul << roots[index];
        }

        float inv = 1f / members.Length;
        _openMask = _rootMask;
        for (int v = 0; v < n; v++)
        {
            _meanLevel[v] *= inv;
            _meanPeak[v] *= inv;
            _meanTalent[v] *= inv;
            if (_meanPeak[v] >= 1f) _openMask |= 1ul << v;
        }
        _weights = math.max(plastSum * inv, 0f);
    }

    private float PhiOfMeans(int n, in SimulationRules.RulesFrame f)
    {
        float phi = 0f;
        for (int v = 0; v < n; v++) phi += SkillGraphMath.PhiTerm(_meanPeak[v], in f);
        return phi;
    }

    private void FillNeeds(ColonyState colony, in SimulationRules.RulesFrame f)
    {
        var stats = colony.Stats;
        _need[(int)SkillNeed.None] = 0f;
        _need[(int)SkillNeed.Hunger] = math.saturate(stats.HungryShare);
        _need[(int)SkillNeed.Danger] = math.saturate(colony.Pressure);
        _need[(int)SkillNeed.Aggression] = math.saturate(stats.PlanShare(PlanKind.Hunt));
        _need[(int)SkillNeed.Mobility] = math.saturate(stats.PlanShare(PlanKind.Migrate) + stats.PlanShare(PlanKind.Seek));
        _need[(int)SkillNeed.Storage] = math.saturate(1f - colony.StorageEma);
        _need[(int)SkillNeed.Care] = math.saturate(colony.JuvenileCount / (float)math.max(colony.MemberCount, 1));
        _need[(int)SkillNeed.Breeding] = math.saturate(1f - colony.PopulationEma / math.max(f.ColonyPopulationNorm, 1f));
    }

    private bool TryPickBuild(SkillGraphTopology topology, SkillAtlas atlas, SimulationRules rules, in SimulationRules.RulesFrame f,
        out int bestU, out int bestV)
    {
        bestU = -1;
        bestV = -1;
        float best = 0f;
        float norm = math.log(1f + rules.SkillSolverNormLevel);
        norm *= norm;
        for (ulong openRest = _openMask; openRest != 0ul; openRest &= openRest - 1ul)
        {
            int u = math.tzcnt(openRest);
            float lu = math.log(1f + _meanLevel[u]);
            float oldFactor = topology.CycleFactor(u);
            float oldBoost = SkillGraphMath.CliqueBoost(topology.CliqueSize(u), in f);
            for (ulong near = atlas.PotentialRow(u) & ~topology.ActiveRow(u); near != 0ul; near &= near - 1ul)
            {
                int v = math.tzcnt(near);
                if (!topology.CanBuild(u, v)) continue;
                topology.PreviewBuild(u, v, out float newFactor, out int newClique);

                float h = lu * math.log(1f + _meanLevel[v]) / norm;
                float s = (SkillGraphMath.CliqueBoost(newClique, in f) - oldBoost) + (oldFactor - newFactor);
                bool island = ((topology.IslandMask >> v) & 1ul) != 0ul;
                float e = _meanLevel[v] < 1f ? _meanTalent[v] * (1f + rules.SkillSolverIslandBonus * (island ? 1f : 0f)) : 0f;
                float c = _need[(int)atlas.SkillAt(v).Need];
                float utility = _weights.x * h + _weights.y * s + _weights.z * e + _weights.w * c;
                if (utility <= best) continue;
                best = utility;
                bestU = u;
                bestV = v;
            }
        }
        return bestU >= 0;
    }

    private bool TryPickPrune(SkillGraphTopology topology, int n, SimulationRules rules, out int bestU, out int bestV)
    {
        bestU = -1;
        bestV = -1;
        float best = rules.SkillPruneUnusedLevel;
        for (int u = 0; u < n; u++)
        for (ulong rest = topology.ActiveRow(u) & ~((2ul << u) - 1ul); rest != 0ul; rest &= rest - 1ul)
        {
            int v = math.tzcnt(rest);
            if (!topology.IsBridge(u, v) || !topology.CanPrune(u, v, -1, out ulong far)) continue;
            if (math.countbits(far) < rules.SkillPruneMinSize || (far & _rootMask) != 0ul) continue;
            float sum = 0f;
            for (ulong side = far; side != 0ul; side &= side - 1ul) sum += _meanLevel[math.tzcnt(side)];
            if (sum >= best) continue;
            best = sum;
            bestU = u;
            bestV = v;
        }
        return bestU >= 0;
    }

    private static void MaterializeMembers(SkillGraphStore store, SkillGraphTopology topology,
        ReadOnlySpan<int> members, in SimulationRules.RulesFrame f)
    {
        float now = SimulationClock.Time;
        for (int i = 0; i < members.Length; i++) store.Materialize(members[i], now, topology, in f);
    }

    private static void Commit(ColonyState colony, SkillGraphStore store, SkillGraphTopology topology, ReadOnlySpan<int> members)
    {
        store.PublishColony(colony.Index, topology);
        for (int i = 0; i < members.Length; i++) store.MarkDirty(members[i]);
    }
}
