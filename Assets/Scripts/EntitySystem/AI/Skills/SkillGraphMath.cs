using Unity.Collections;
using Unity.Mathematics;

public static class SkillGraphMath
{
    public static int RootVertex(float2 origin, ulong islandMask, in NativeArray<float2> positions)
    {
        int best = -1;
        float bestD = float.MaxValue;
        for (int v = 0; v < positions.Length; v++)
        {
            if (((islandMask >> v) & 1ul) != 0ul) continue;
            float d = math.distancesq(positions[v], origin);
            if (d < bestD) { bestD = d; best = v; }
        }
        return best < 0 ? 0 : best;
    }

    public static float Talent(float2 origin, float2 position, int treeDistance, int treeDiameter, in SimulationRules.RulesFrame f)
    {
        float euclid = math.distance(position, origin) * math.rsqrt(2f);
        float tree = treeDiameter > 0 ? math.saturate((float)treeDistance / treeDiameter) : 0f;
        float share = math.saturate(f.SkillTalentEuclidShare);
        float d = share * math.saturate(euclid) + (1f - share) * tree;
        return f.SkillTalentMin + (1f - f.SkillTalentMin) * math.exp(-f.SkillTalentDecay * d);
    }

    public static bool IsOpen(float peak, int vertex, int root) => peak >= 1f || vertex == root;

    public static bool IsAvailable(int vertex, int root, ulong rootComponent, ulong prunedMask,
        in NativeArray<ulong> active, in NativeArray<float> peaks, int peakBase)
    {
        ulong bit = 1ul << vertex;
        if ((rootComponent & bit) == 0ul || (prunedMask & bit) != 0ul) return false;
        if (IsOpen(peaks[peakBase + vertex], vertex, root)) return true;
        for (ulong near = active[vertex]; near != 0ul; near &= near - 1ul)
        {
            int u = math.tzcnt(near);
            if (IsOpen(peaks[peakBase + u], u, root)) return true;
        }
        return false;
    }

    public static ulong OpenMask(in NativeArray<float> peaks, int peakBase, int count, int root)
    {
        ulong mask = 0ul;
        for (int v = 0; v < count; v++)
            if (IsOpen(peaks[peakBase + v], v, root)) mask |= 1ul << v;
        return mask;
    }

    public static ulong AvailableMask(ulong openMask, ulong rootComponent, ulong prunedMask, in NativeArray<ulong> active)
    {
        ulong reach = openMask;
        for (ulong rest = openMask; rest != 0ul; rest &= rest - 1ul) reach |= active[math.tzcnt(rest)];
        return reach & rootComponent & ~prunedMask;
    }

    public static ulong SilhouetteMask(ulong openMask, in NativeArray<ulong> active, in NativeArray<ulong> potential)
    {
        ulong mask = 0ul;
        for (ulong rest = openMask; rest != 0ul; rest &= rest - 1ul)
        {
            int v = math.tzcnt(rest);
            mask |= potential[v] & ~active[v];
        }
        return mask & ~openMask;
    }

    public static float PhiTerm(float peak, in SimulationRules.RulesFrame f)
    {
        float p = math.max(peak, 0f);
        return math.log(1f + p * p / (p + f.SkillPhiRefLevel * f.SkillPhiRefLevel));
    }

    public static float Phi(in NativeArray<float> peaks, int peakBase, int count, in SimulationRules.RulesFrame f)
    {
        float sum = 0f;
        for (int v = 0; v < count; v++) sum += PhiTerm(peaks[peakBase + v], in f);
        return sum;
    }

    public static int Earned(float phiPeak, in SimulationRules.RulesFrame f)
    {
        float x = phiPeak / f.SkillEdgeCostScale;
        if (!(x > 0f)) return 0;
        return (int)math.floor(math.pow(x, 1f / f.SkillEdgeCostExp));
    }

    public static float CliqueBoost(int cliqueSize, in SimulationRules.RulesFrame f)
        => cliqueSize == 3 ? f.SkillClique3Boost : (cliqueSize == 4 ? f.SkillClique4Boost : 1f);

    public static float BlockScore(int edges, int vertices)
        => (edges - vertices + 1) * math.log(1f + vertices);

    public static float CycleFactor(float blockScore, in SimulationRules.RulesFrame f)
        => blockScore <= 0f ? 1f : f.SkillCycleMinFactor + (1f - f.SkillCycleMinFactor) / (1f + f.SkillCycleScale * blockScore);

    public static float Decay(float level, float peak, float dt, float cycleFactor, in SimulationRules.RulesFrame f)
    {
        float floor = f.SkillSavingsFloor * peak;
        if (!(dt > 0f) || level <= floor) return level;
        return floor + (level - floor) * math.exp(-f.SkillDecayRate * cycleFactor * dt);
    }

    public static float EnvironmentDelta(float level, float peak, int vertex, ulong activeRow,
        in NativeArray<float> levels, int levelBase, in NativeArray<float2> positions, float meanEdgeLength)
    {
        float d = math.max(peak - level, 0f);
        float2 xv = positions[vertex];
        for (ulong near = activeRow; near != 0ul; near &= near - 1ul)
        {
            int u = math.tzcnt(near);
            float gap = levels[levelBase + u] - level;
            if (gap <= 0f) continue;
            float len = math.max(math.distance(positions[u], xv), 1e-4f);
            d = math.max(d, meanEdgeLength / len * gap);
        }
        return d;
    }

    public static float LogicBoost(float environmentDelta, float level, in SimulationRules.RulesFrame f)
        => 1f + f.SkillLogicScale * math.log(1f + environmentDelta / (level + f.SkillLogicOffset));

    public static float FocusBoost(int prunedCount, int skillCount, in SimulationRules.RulesFrame f)
        => 1f + f.SkillPruneFocusScale * prunedCount / math.max(skillCount, 1);

    public static float XpGain(float xp, float level, float2 xpCost, float talent, float boost, in SimulationRules.RulesFrame f)
        => xp / (xpCost.x * math.pow(level + f.SkillXpLevelOffset, xpCost.y)) * talent * boost;

    public static float Effect(float level, float3 curve, bool divider)
    {
        float m = 1f + curve.z * (math.pow(math.floor(math.max(level, 0f)) + curve.y, curve.x) - math.pow(curve.y, curve.x));
        return divider ? 1f / m : m;
    }
}
