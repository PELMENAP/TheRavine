using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class SkillGraphTests
{
    private SimulationRules _rules;
    private SkillAtlas _atlas;
    private SkillAtlas _grid;
    private SkillGraphTopology _topology;

    [SetUp]
    public void SetUp()
    {
        _rules = TestRules.Bind();
        _atlas = SkillAtlas.BuildDefault();
        _grid = BuildGridAtlas();
    }

    [TearDown]
    public void TearDown()
    {
        _topology?.Dispose();
        _topology = null;
        Object.DestroyImmediate(_atlas);
        Object.DestroyImmediate(_grid);
        TestRules.Release(_rules);
    }

    private static SkillAtlas BuildGridAtlas(SkillChannel channel = SkillChannel.None, bool divider = false)
        => SkillGraphTestAtlas.BuildGrid(channel, divider);

    private static void Link(ulong[] rows, int a, int b)
    {
        rows[a] |= 1ul << b;
        rows[b] |= 1ul << a;
    }

    private SkillGraphTopology Topology(SkillAtlas atlas, float4 genes = default)
    {
        _topology?.Dispose();
        _topology = SkillGraphTopology.Create(atlas, genes);
        return _topology;
    }

    private static bool Connected(ulong[] rows, int count, int a, int b, int skipU = -1, int skipV = -1)
    {
        ulong seen = 1ul << a, frontier = seen;
        while (frontier != 0ul)
        {
            int v = math.tzcnt(frontier);
            frontier &= frontier - 1ul;
            ulong row = rows[v];
            if (v == skipU) row &= ~(1ul << skipV);
            if (v == skipV) row &= ~(1ul << skipU);
            ulong fresh = row & ~seen;
            seen |= fresh;
            frontier |= fresh;
        }
        return ((seen >> b) & 1ul) != 0ul;
    }

    private static ulong[] RowsOf(SkillGraphTopology t)
    {
        var rows = new ulong[t.Count];
        for (int v = 0; v < t.Count; v++) rows[v] = t.ActiveRow(v);
        return rows;
    }

    [Test]
    public void DefaultAtlasHasEighteenPointsInsideTheUnitSquare()
    {
        Assert.AreEqual(18, _atlas.Count);
        for (int i = 0; i < _atlas.Count; i++)
        {
            var p = _atlas.PositionOf(i);
            Assert.That(p.x, Is.InRange(0f, 1f));
            Assert.That(p.y, Is.InRange(0f, 1f));
        }
        Assert.IsTrue(_atlas.Validate(out string error), error);
    }

    [Test]
    public void DefaultAtlasPotentialIsSymmetricWithoutSelfLoops()
    {
        for (int u = 0; u < _atlas.Count; u++)
        {
            Assert.AreEqual(0ul, (_atlas.PotentialRow(u) >> u) & 1ul, "self loop at " + u);
            for (int v = 0; v < _atlas.Count; v++)
                Assert.AreEqual((_atlas.PotentialRow(u) >> v) & 1ul, (_atlas.PotentialRow(v) >> u) & 1ul);
        }
    }

    [Test]
    public void DefaultAtlasPotentialIsConnectedAndHasNoK5()
    {
        var rows = new ulong[_atlas.Count];
        for (int u = 0; u < rows.Length; u++) rows[u] = _atlas.PotentialRow(u);
        for (int v = 1; v < rows.Length; v++) Assert.IsTrue(Connected(rows, rows.Length, 0, v), "vertex " + v);
        Assert.IsFalse(SkillAtlasBuilder.HasK5(rows, rows.Length));
    }

    [Test]
    public void K5CheckFindsACompleteFiveClique()
    {
        var rows = new ulong[16];
        for (int a = 0; a < 5; a++)
        for (int b = a + 1; b < 5; b++) Link(rows, a, b);
        Assert.IsTrue(SkillAtlasBuilder.HasK5(rows, 16));
        rows[0] &= ~(1ul << 4);
        rows[4] &= ~1ul;
        Assert.IsFalse(SkillAtlasBuilder.HasK5(rows, 16));
    }

    [Test]
    public void BuilderProducesValidAtlasesForEverySizeAndSeed()
    {
        foreach (int count in new[] { 16, 24, 40, 64 })
        for (uint seed = 1; seed <= 3; seed++)
        {
            SkillAtlasBuilder.Generate(count, seed, SimulationRules.DefaultSkillK4Quantile, out var points, out var potential);
            Assert.AreEqual(count, points.Length);
            Assert.IsFalse(SkillAtlasBuilder.HasK5(potential, count), count + "/" + seed);
        }
    }

    [Test]
    public void XpTableHasAtMostTwoSourcesPerAction()
    {
        int total = 0;
        for (int a = 0; a < ActionCatalog.Count; a++)
            if (_atlas.TryGetXpRange(a, out _, out int length))
            {
                Assert.LessOrEqual(length, SkillAtlas.MaxXpPerAction);
                total += length;
            }
        Assert.AreEqual(_atlas.XpSourceCount, total);
        Assert.IsFalse(_atlas.TryGetXpRange((int)EntityAction.Idle, out _, out _));
        Assert.IsFalse(_atlas.TryGetXpRange(-1, out _, out _));
    }

    [Test]
    public void FreshTreeHasNoCycleFactorBelowOne()
    {
        var t = Topology(_atlas, new float4(0.1f, -0.2f, 0.05f, 0.2f));
        for (int v = 0; v < t.Count; v++) Assert.AreEqual(1f, t.CycleFactor(v), "vertex " + v);
        for (int v = 0; v < t.Count; v++) Assert.LessOrEqual(t.CliqueSize(v), 2);
    }

    [Test]
    public void IslandsAreDetachedFromTheMainComponent()
    {
        var t = Topology(_atlas);
        if (t.IslandMask == 0ul) Assert.Pass("no island for this forest");
        ulong main = 0ul;
        for (int v = 0; v < t.Count; v++)
            if (((t.IslandMask >> v) & 1ul) == 0ul) main = t.ComponentOf(v);
        Assert.AreEqual(0ul, main & t.IslandMask);
        Assert.AreEqual(t.Count - math.countbits(t.IslandMask), math.countbits(main));
    }

    [Test]
    public void OneAddedEdgeFormingATriangleGivesCliqueThreeForExactlyThreeVertices()
    {
        var t = Topology(_grid);
        t.UpdatePhi(1000f);
        foreach (var (u, v) in new[] { (0, 1), (1, 5), (0, 5) })
            if (!t.IsActive(u, v)) Assert.IsTrue(t.TryBuild(u, v), u + "-" + v);

        int triple = 0;
        for (int v = 0; v < t.Count; v++)
        {
            Assert.LessOrEqual(t.CliqueSize(v), 3);
            if (t.CliqueSize(v) == 3) triple++;
        }
        Assert.AreEqual(3, triple);
        Assert.AreEqual(3, t.CliqueSize(0));
        Assert.AreEqual(3, t.CliqueSize(1));
        Assert.AreEqual(3, t.CliqueSize(5));
        Assert.Less(t.CycleFactor(0), 1f);
    }

    [Test]
    public void ThreeAddedEdgesFormingK4GiveCliqueFour()
    {
        var t = Topology(_grid);
        t.UpdatePhi(1000f);
        foreach (var (u, v) in new[] { (0, 1), (0, 4), (0, 5), (1, 4), (1, 5), (4, 5) })
            if (!t.IsActive(u, v)) Assert.IsTrue(t.TryBuild(u, v), u + "-" + v);

        int quad = 0;
        for (int v = 0; v < t.Count; v++)
            if (t.CliqueSize(v) == 4) quad++;
        Assert.AreEqual(4, quad);
        foreach (int v in new[] { 0, 1, 4, 5 }) Assert.AreEqual(4, t.CliqueSize(v));
    }

    [Test]
    public void BridgeIsReportedOnlyForEdgesOutsideCycles()
    {
        foreach (var atlas in new[] { _grid, _atlas })
        {
            var t = Topology(atlas, new float4(0.2f, 0.1f, -0.1f, 0.2f));
            t.UpdatePhi(1000f);
            for (int tries = 0; tries < 40; tries++)
            {
                int u = RavineRandom.RangeInt(t.Count), v = RavineRandom.RangeInt(t.Count);
                t.TryBuild(u, v);
            }
            var rows = RowsOf(t);
            for (int u = 0; u < t.Count; u++)
            for (int v = u + 1; v < t.Count; v++)
            {
                if (!t.IsActive(u, v)) { Assert.IsFalse(t.IsBridge(u, v)); continue; }
                bool bridge = !Connected(rows, t.Count, u, v, u, v);
                Assert.AreEqual(bridge, t.IsBridge(u, v), u + "-" + v);
            }
        }
    }

    [Test]
    public void BuildIsRefusedWithoutPointsAndLeavesStateUnchanged()
    {
        var t = Topology(_grid);
        int u = -1, v = -1;
        for (int a = 0; a < t.Count && u < 0; a++)
        for (int b = a + 1; b < t.Count; b++)
            if (_grid.HasPotentialEdge(a, b) && !t.IsActive(a, b)) { u = a; v = b; break; }
        Assert.GreaterOrEqual(u, 0);

        int revision = t.Revision;
        Assert.IsFalse(t.TryBuild(u, v));
        Assert.AreEqual(revision, t.Revision);
        Assert.IsFalse(t.TryBuild(u, u));
        Assert.IsFalse(t.TryBuild(-1, 3));
        Assert.IsFalse(t.TryBuild(0, 64));

        t.UpdatePhi(1000f);
        int spent = t.SpentEdgePoints;
        Assert.IsTrue(t.TryBuild(u, v));
        Assert.AreEqual(revision + 1, t.Revision);
        Assert.AreEqual(spent + SkillGraphTopology.BuildCost, t.SpentEdgePoints);
        Assert.IsFalse(t.TryBuild(u, v));
    }

    [Test]
    public void PruneAndRestoreFollowTheBridgeRules()
    {
        var t = Topology(_grid);
        t.UpdatePhi(1000f);
        int root = 0;
        while (((t.IslandMask >> root) & 1ul) != 0ul) root++;

        int bu = -1, bv = -1;
        for (int u = 0; u < t.Count && bu < 0; u++)
        for (int v = u + 1; v < t.Count; v++)
            if (t.IsActive(u, v) && t.CanPrune(u, v, root)) { bu = u; bv = v; break; }
        Assert.GreaterOrEqual(bu, 0, "no prunable bridge");

        int revision = t.Revision;
        Assert.IsTrue(t.TryPrune(bu, bv, root));
        Assert.AreEqual(revision + 1, t.Revision);
        Assert.AreNotEqual(0ul, t.PrunedMask);
        Assert.AreEqual(0ul, t.PrunedMask & (1ul << root));
        Assert.AreEqual(1, t.PrunedBridgeCount);
        Assert.IsFalse(t.IsActive(bu, bv));
        Assert.IsFalse(t.TryPrune(bu, bv, root));
        Assert.IsFalse(t.TryBuild(bu, bv));

        int spent = t.SpentEdgePoints;
        Assert.IsTrue(t.TryRestore(bv, bu));
        Assert.AreEqual(0ul, t.PrunedMask);
        Assert.AreEqual(0, t.PrunedBridgeCount);
        Assert.IsTrue(t.IsActive(bu, bv));
        Assert.AreEqual(spent + SkillGraphTopology.RestoreCost, t.SpentEdgePoints);
        Assert.IsFalse(t.TryRestore(bu, bv));
    }

    [Test]
    public void PruneRefusesACycleEdgeAndAnIsland()
    {
        var t = Topology(_grid);
        t.UpdatePhi(1000f);
        foreach (var (u, v) in new[] { (0, 1), (1, 5), (0, 5) })
            if (!t.IsActive(u, v)) t.TryBuild(u, v);

        int revision = t.Revision;
        Assert.IsFalse(t.TryPrune(0, 1, -1));
        Assert.IsFalse(t.TryPrune(1, 5, -1));
        Assert.IsFalse(t.TryPrune(0, 5, -1));
        Assert.IsFalse(t.TryPrune(0, 15, -1));
        Assert.AreEqual(revision, t.Revision);
        Assert.AreEqual(0ul, t.PrunedMask);
    }

    [Test]
    public void TalentIsOneAtTheOriginVertexAndNeverBelowTheMinimum()
    {
        var t = Topology(_atlas);
        ref readonly var f = ref SimulationRules.Frame;
        var positions = _atlas.NativePositions;
        int origin = 0;
        while (((t.IslandMask >> origin) & 1ul) != 0ul) origin++;
        float2 o = positions[origin];
        int root = SkillGraphMath.RootVertex(o, t.IslandMask, in positions);
        Assert.AreEqual(origin, root);
        Assert.AreEqual(1f, SkillGraphMath.Talent(o, positions[origin], 0, t.TreeDiameter, in f), 1e-5f);

        for (int v = 0; v < t.Count; v++)
            Assert.GreaterOrEqual(SkillGraphMath.Talent(o, positions[v], t.TreeDistance(root, v), t.TreeDiameter, in f), f.SkillTalentMin - 1e-6f);
        Assert.GreaterOrEqual(SkillGraphMath.Talent(new float2(1f, 1f), new float2(0f, 0f), 255, t.TreeDiameter, in f), f.SkillTalentMin - 1e-6f);
    }

    [Test]
    public void SixtyFourSkillsAtPeakOneEarnNoEdgePoints()
    {
        ref readonly var f = ref SimulationRules.Frame;
        var peaks = new NativeArray<float>(64, Allocator.Temp);
        try
        {
            for (int i = 0; i < 64; i++) peaks[i] = 1f;
            float phi = SkillGraphMath.Phi(in peaks, 0, 64, in f);
            Assert.AreEqual(0, SkillGraphMath.Earned(phi, in f));
            for (int i = 0; i < 64; i++) peaks[i] = 100f;
            Assert.Greater(SkillGraphMath.Earned(SkillGraphMath.Phi(in peaks, 0, 64, in f), in f), 0);
        }
        finally
        {
            peaks.Dispose();
        }
    }

    [Test]
    public void DecayNeverCrossesTheSavingsFloor()
    {
        ref readonly var f = ref SimulationRules.Frame;
        float floor = f.SkillSavingsFloor * 100f;
        foreach (float r in new[] { 0.25f, 0.5f, 1f })
        foreach (float dt in new[] { 0f, 1f, 600f, 1e6f })
        {
            float l = SkillGraphMath.Decay(100f, 100f, dt, r, in f);
            Assert.GreaterOrEqual(l, floor - 1e-4f);
            Assert.LessOrEqual(l, 100f + 1e-4f);
        }
        Assert.AreEqual(floor, SkillGraphMath.Decay(100f, 100f, 1e9f, 1f, in f), 1e-3f);
        Assert.AreEqual(10f, SkillGraphMath.Decay(10f, 100f, 1e3f, 1f, in f));
    }

    [Test]
    public void CycleFactorLowersDecayInsideCyclesOnly()
    {
        ref readonly var f = ref SimulationRules.Frame;
        Assert.AreEqual(1f, SkillGraphMath.CycleFactor(0f, in f));
        float s = SkillGraphMath.BlockScore(3, 3);
        float r = SkillGraphMath.CycleFactor(s, in f);
        Assert.Less(r, 1f);
        Assert.GreaterOrEqual(r, f.SkillCycleMinFactor);
    }

    [Test]
    public void FallenSkillWithoutNeighboursGetsAPullAboveOnePointSix()
    {
        ref readonly var f = ref SimulationRules.Frame;
        var levels = new NativeArray<float>(1, Allocator.Temp);
        var positions = new NativeArray<float2>(1, Allocator.Temp);
        try
        {
            levels[0] = 25f;
            float d = SkillGraphMath.EnvironmentDelta(25f, 100f, 0, 0ul, in levels, 0, in positions, 0.2f);
            Assert.AreEqual(75f, d, 1e-4f);
            Assert.Greater(SkillGraphMath.LogicBoost(d, 25f, in f), 1.6f);
        }
        finally
        {
            levels.Dispose();
            positions.Dispose();
        }
    }

    [Test]
    public void NeighbourAbovePullsAHigherDelta()
    {
        ref readonly var f = ref SimulationRules.Frame;
        var levels = new NativeArray<float>(2, Allocator.Temp);
        var positions = new NativeArray<float2>(2, Allocator.Temp);
        try
        {
            levels[0] = 10f;
            levels[1] = 50f;
            positions[0] = new float2(0f, 0f);
            positions[1] = new float2(0.1f, 0f);
            float d = SkillGraphMath.EnvironmentDelta(10f, 10f, 0, 2ul, in levels, 0, in positions, 0.2f);
            Assert.AreEqual(0.2f / 0.1f * 40f, d, 1e-3f);
        }
        finally
        {
            levels.Dispose();
            positions.Dispose();
        }
    }

    [Test]
    public void EffectGrowsWithTheFlooredLevelAndDividersInvert()
    {
        var curve = new float3(0.8f, 6f, 0.12f);
        Assert.AreEqual(1f, SkillGraphMath.Effect(0f, curve, false), 1e-6f);
        float low = SkillGraphMath.Effect(10.9f, curve, false);
        Assert.AreEqual(low, SkillGraphMath.Effect(10f, curve, false), 1e-6f);
        Assert.Greater(SkillGraphMath.Effect(11f, curve, false), low);
        Assert.AreEqual(1f / low, SkillGraphMath.Effect(10.5f, curve, true), 1e-6f);
    }

    [Test]
    public void AddXpOnAnUnavailableSkillChangesNothing()
    {
        var t = Topology(_atlas);
        var store = new SkillGraphStore(2, _atlas.Count);
        try
        {
            float2 origin = _atlas.PositionOf(0);
            store.Reset(0, origin, t, _atlas);
            store.ClearDirty(0);
            int root = store.Root(0);

            int far = -1;
            for (int v = 0; v < t.Count; v++)
                if (v != root && ((t.ActiveRow(root) >> v) & 1ul) == 0ul) { far = v; break; }
            Assert.GreaterOrEqual(far, 0);

            bool changed = store.AddXp(0, far, 100f, 10f, t, _atlas, in SimulationRules.Frame);
            Assert.IsFalse(changed);
            Assert.AreEqual(0f, store.Level(0, far));
            Assert.AreEqual(0f, store.Peak(0, far));
            Assert.IsFalse(store.IsDirty(0));
        }
        finally
        {
            store.Dispose();
        }
    }

    [Test]
    public void AddXpOnTheRootRaisesTheLevelAndEventuallyTheFlooredLevel()
    {
        var t = Topology(_atlas);
        var store = new SkillGraphStore(1, _atlas.Count);
        try
        {
            store.Reset(0, _atlas.PositionOf(0), t, _atlas);
            store.ClearDirty(0);
            int root = store.Root(0);
            Assert.IsTrue(store.Talent(0, root) >= SimulationRules.Frame.SkillTalentMin);

            bool changed = false;
            for (int i = 0; i < 50 && !changed; i++)
                changed = store.AddXp(0, root, 1f, i, t, _atlas, in SimulationRules.Frame);
            Assert.IsTrue(changed);
            Assert.GreaterOrEqual(store.Level(0, root), 1f);
            Assert.AreEqual(store.Level(0, root), store.Peak(0, root));
            Assert.IsTrue(store.IsDirty(0));
        }
        finally
        {
            store.Dispose();
        }
    }

    [Test]
    public void AddXpNeverAllocates()
    {
        var t = Topology(_atlas);
        var store = new SkillGraphStore(1, _atlas.Count);
        try
        {
            store.Reset(0, _atlas.PositionOf(0), t, _atlas);
            int root = store.Root(0);
            float now = 0f;
            AllocationAssert.Zero(() =>
            {
                now += 1f;
                store.AddXp(0, root, 1f, now, t, _atlas, in SimulationRules.Frame);
            }, 200, "SkillGraphStore.AddXp");
        }
        finally
        {
            store.Dispose();
        }
    }

    [Test]
    public void ModifiersAreNeutralUntilAChannelSkillLevelsUp()
    {
        using var attack = new AtlasHandle(BuildGridAtlas(SkillChannel.Attack, false));
        using var cooldown = new AtlasHandle(BuildGridAtlas(SkillChannel.Cooldown, true));
        foreach (var handle in new[] { attack, cooldown })
        {
            var t = Topology(handle.Atlas);
            var store = new SkillGraphStore(1, handle.Atlas.Count);
            try
            {
                store.Reset(0, handle.Atlas.PositionOf(0), t, handle.Atlas);
                int root = store.Root(0);
                var neutral = SkillModifiers.From(store, 0, t, handle.Atlas);
                Assert.AreEqual(1f, neutral.Attack);
                Assert.AreEqual(1f, neutral.Cooldown);

                store.SetLevel(0, root, 40f, 0f);
                var raised = SkillModifiers.From(store, 0, t, handle.Atlas);
                if (handle == attack) Assert.Greater(raised.Attack, 1f);
                else Assert.Less(raised.Cooldown, 1f);
                Assert.AreEqual(1f, raised.Speed);
            }
            finally
            {
                store.Dispose();
            }
        }
    }

    [Test]
    public void ModifiersIgnorePrunedSkills()
    {
        using var handle = new AtlasHandle(BuildGridAtlas(SkillChannel.Attack, false));
        var t = Topology(handle.Atlas);
        t.UpdatePhi(1000f);
        int root = 0;
        while (((t.IslandMask >> root) & 1ul) != 0ul) root++;

        int bu = -1, bv = -1;
        for (int u = 0; u < t.Count && bu < 0; u++)
        for (int v = u + 1; v < t.Count; v++)
            if (t.IsActive(u, v) && t.CanPrune(u, v, root)) { bu = u; bv = v; break; }
        Assert.GreaterOrEqual(bu, 0);

        var store = new SkillGraphStore(1, handle.Atlas.Count);
        try
        {
            store.Reset(0, handle.Atlas.PositionOf(root), t, handle.Atlas);
            Assert.AreEqual(root, store.Root(0));
            Assert.IsTrue(t.TryPrune(bu, bv, root));
            ulong pruned = t.PrunedMask;
            for (int v = 0; v < t.Count; v++)
                if (((pruned >> v) & 1ul) != 0ul) store.SetLevel(0, v, 60f, 0f);
            Assert.AreEqual(1f, SkillModifiers.From(store, 0, t, handle.Atlas).Attack);

            store.SetLevel(0, root, 60f, 0f);
            Assert.Greater(SkillModifiers.From(store, 0, t, handle.Atlas).Attack, 1f);
        }
        finally
        {
            store.Dispose();
        }
    }

    private sealed class AtlasHandle : System.IDisposable
    {
        public readonly SkillAtlas Atlas;
        public AtlasHandle(SkillAtlas atlas) => Atlas = atlas;
        public void Dispose() => Object.DestroyImmediate(Atlas);
    }
}
