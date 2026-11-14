using Unity.Mathematics;
using UnityEngine;

public static class SkillGraphQuery
{
    public static int Checksum(ISkillGraphSource source)
    {
        var store = source.Store;
        int index = source.StoreIndex;
        int n = source.Atlas.Count;
        unchecked
        {
            int hash = source.Topology.Revision * 397 ^ source.StateVersion;
            for (int v = 0; v < n; v++) hash = hash * 31 + (int)math.floor(store.Level(index, v) * 10f);
            return hash;
        }
    }

    public static void Fill(ISkillGraphSource source, SkillGraphSnapshot snapshot)
    {
        var atlas = source.Atlas;
        var topology = source.Topology;
        var store = source.Store;
        int index = source.StoreIndex;
        int n = atlas.Count;
        ref readonly var f = ref SimulationRules.Frame;

        snapshot.Clear();
        var peaks = store.Peaks;
        var levels = store.Levels;
        var active = topology.ActiveRows;
        var potential = atlas.NativePotential;
        var positions = atlas.NativePositions;
        int baseIndex = index * n;
        int root = store.Root(index);

        ulong pruned = topology.PrunedMask;
        ulong rootComponent = topology.ComponentOf(root);
        ulong open = SkillGraphMath.OpenMask(in peaks, baseIndex, n, root) & ~pruned;
        ulong available = SkillGraphMath.AvailableMask(open, rootComponent, pruned, in active) & ~open;
        ulong silhouette = SkillGraphMath.SilhouetteMask(open, in active, in potential);
        ulong everOpen = SkillGraphMath.OpenMask(in peaks, baseIndex, n, root);

        ulong visible = 0ul;
        snapshot.FocusBoost = SkillGraphMath.FocusBoost(math.countbits(pruned), n, in f);
        for (int v = 0; v < n; v++)
        {
            ulong bit = 1ul << v;
            SkillVertexState state;
            if ((open & bit) != 0ul) state = SkillVertexState.Open;
            else if ((pruned & bit) != 0ul) state = ((silhouette | everOpen) & bit) != 0ul ? SkillVertexState.Pruned : SkillVertexState.Hidden;
            else if ((available & bit) != 0ul) state = SkillVertexState.Available;
            else if ((silhouette & bit) != 0ul) state = (topology.IslandMask & bit) != 0ul ? SkillVertexState.Island : SkillVertexState.Silhouette;
            else state = SkillVertexState.Hidden;
            if (state == SkillVertexState.Hidden) continue;

            visible |= bit;
            int slot = snapshot.AddVertex(v);
            float level = levels[baseIndex + v];
            float peak = peaks[baseIndex + v];
            float floor = math.floor(level);
            snapshot.Position[slot] = new Vector2(positions[v].x, positions[v].y);
            snapshot.Level[slot] = (int)floor;
            snapshot.Fraction[slot] = level - floor;
            snapshot.Peak[slot] = peak;
            snapshot.Floor[slot] = f.SkillSavingsFloor * peak;
            snapshot.Talent[slot] = store.Talent(index, v);
            snapshot.State[slot] = state;
            snapshot.Clique[slot] = (byte)topology.CliqueSize(v);
            snapshot.Decay[slot] = topology.CycleFactor(v);
            snapshot.CliqueBoost[slot] = SkillGraphMath.CliqueBoost(topology.CliqueSize(v), in f);
            float pull = SkillGraphMath.EnvironmentDelta(level, peak, v, active[v], in levels, baseIndex, in positions, atlas.MeanEdgeLength);
            snapshot.LogicBoost[slot] = SkillGraphMath.LogicBoost(pull, level, in f);
        }

        for (ulong rest = visible; rest != 0ul; rest &= rest - 1ul)
        {
            int u = math.tzcnt(rest);
            for (ulong near = potential[u] & visible & ~((2ul << u) - 1ul); near != 0ul; near &= near - 1ul)
            {
                int w = math.tzcnt(near);
                if (((open >> u) & 1ul) == 0ul && ((open >> w) & 1ul) == 0ul) continue;
                SkillEdgeKind kind = topology.IsActive(u, w)
                    ? (topology.IsBridge(u, w) ? SkillEdgeKind.Bridge : SkillEdgeKind.Active)
                    : SkillEdgeKind.Potential;
                snapshot.AddEdge(u, w, kind);
            }
        }

        snapshot.Revision = topology.Revision;
        snapshot.StateVersion = source.StateVersion;
        snapshot.AutoMode = source.AutoMode;
        snapshot.PhiPeak = topology.PhiPeak;
        snapshot.Earned = topology.Earned(in f);
        snapshot.Spent = topology.SpentEdgePoints;
        snapshot.Available = math.max(snapshot.Earned - snapshot.Spent, 0);
        snapshot.LevelChecksum = Checksum(source);
    }
}
