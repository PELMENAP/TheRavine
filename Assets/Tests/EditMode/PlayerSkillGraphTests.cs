using NUnit.Framework;
using TheRavine.Base;
using TheRavine.EntityControl;
using Unity.Mathematics;
using UnityEngine;

public class PlayerSkillGraphTests
{
    private SimulationRules _rules;
    private SkillAtlas _atlas;
    private PlayerSkillGraph _graph;
    private PlayerSkillGraph _mirror;

    [SetUp]
    public void SetUp()
    {
        _rules = TestRules.Bind();
        _atlas = SkillAtlas.BuildDefault();
        var genes = GeneticParameters.Default;
        _graph = PlayerSkillGraph.Create(_atlas, ref genes, null, true);
        var mirrorGenes = GeneticParameters.Default;
        _mirror = PlayerSkillGraph.Create(_atlas, ref mirrorGenes, null, false);
    }

    [TearDown]
    public void TearDown()
    {
        _graph.Dispose();
        _mirror.Dispose();
        Object.DestroyImmediate(_atlas);
        TestRules.Release(_rules);
    }

    private void FreePoints() => _graph.Topology.UpdatePhi(1000f);

    private bool FindBuildable(out int u, out int v)
    {
        u = _graph.Store.Root(PlayerSkillGraph.Slot);
        for (v = 0; v < _atlas.Count; v++)
            if (v != u && _atlas.HasPotentialEdge(u, v) && !_graph.Topology.IsActive(u, v) && ((_graph.Topology.PrunedMask >> v) & 1ul) == 0ul)
                return true;
        return false;
    }

    private (int revision, int spent, ulong pruned, int version) Snapshot()
        => (_graph.Topology.Revision, _graph.Topology.SpentEdgePoints, _graph.Topology.PrunedMask, _graph.StateVersion);

    [Test]
    public void DrawnGenesStayInsideTheParameterRanges()
    {
        for (int i = 0; i < 100; i++)
        {
            var genes = GeneticParameters.Default;
            PlayerSkillGraph.DrawGenes(ref genes);
            Assert.That(genes.SkillOriginX, Is.InRange(0f, 1f));
            Assert.That(genes.SkillOriginY, Is.InRange(0f, 1f));
            Assert.That(genes.PlastHebb, Is.InRange(0f, 2f));
            Assert.That(genes.PlastStruct, Is.InRange(0f, 2f));
            Assert.That(genes.PlastExplore, Is.InRange(0f, 2f));
            Assert.That(genes.PlastNeed, Is.InRange(0f, 2f));
        }
    }

    [Test]
    public void ValidationRefusesBadArgumentsWithoutChangingState()
    {
        FreePoints();
        var before = Snapshot();
        int n = _atlas.Count;
        int u = _graph.Store.Root(PlayerSkillGraph.Slot);

        Assert.IsFalse(_graph.TryExecute((SkillCommandOp)200, 0, 1, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Count, 0, 1, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, n, 0, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, 0, 255, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, -1, 3, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, int.MinValue, int.MaxValue, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, u, u, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Prune, n, n, 1f));
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Restore, 0, 1, 1f));

        for (int v = 0; v < n; v++)
            if (v != u && !_atlas.HasPotentialEdge(u, v)) Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, u, v, 1f), "not in potential " + v);
        for (int a = 0; a < n; a++)
        for (int b = 0; b < n; b++)
            if (a != b && !_graph.Topology.IsActive(a, b)) Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Prune, a, b, 1f));

        Assert.AreEqual(before, Snapshot());
    }

    [Test]
    public void BuildNeedsAnOpenEndpointAndEdgePoints()
    {
        Assert.IsTrue(FindBuildable(out int u, out int v));
        var before = Snapshot();
        Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, u, v, 1f), "no edge points yet");
        Assert.AreEqual(before, Snapshot());

        FreePoints();
        int far = -1;
        for (int a = 0; a < _atlas.Count && far < 0; a++)
        for (int b = a + 1; b < _atlas.Count; b++)
            if (a != u && b != u && !_graph.IsOpen(a) && !_graph.IsOpen(b) && _atlas.HasPotentialEdge(a, b) && !_graph.Topology.IsActive(a, b))
            {
                far = a * 256 + b;
                break;
            }
        if (far >= 0) Assert.IsFalse(_graph.TryExecute(SkillCommandOp.Build, far / 256, far % 256, 1f), "no open endpoint");
        Assert.AreEqual(before.revision, _graph.Topology.Revision);

        Assert.IsTrue(_graph.TryExecute(SkillCommandOp.Build, u, v, 1f));
        Assert.AreEqual(before.revision + 1, _graph.Topology.Revision);
        Assert.AreEqual(before.spent + SkillGraphTopology.BuildCost, _graph.Topology.SpentEdgePoints);
        Assert.AreEqual(before.version + 1, _graph.StateVersion);
        Assert.IsTrue(_graph.Topology.IsActive(u, v));
    }

    [Test]
    public void AMirrorNeverExecutesCommands()
    {
        _mirror.Topology.UpdatePhi(1000f);
        int revision = _mirror.Topology.Revision;
        for (int u = 0; u < _atlas.Count; u++)
        for (int v = 0; v < _atlas.Count; v++)
            Assert.IsFalse(_mirror.TryExecute(SkillCommandOp.Build, u, v, 1f));
        Assert.IsFalse(_mirror.TryExecute(SkillCommandOp.ToggleAuto, 0, 0, 1f));
        Assert.AreEqual(revision, _mirror.Topology.Revision);
    }

    [Test]
    public void ToggleAndSyncFollowTheProtocol()
    {
        Assert.IsFalse(_graph.AutoMode);
        int version = _graph.StateVersion;
        Assert.IsTrue(_graph.TryExecute(SkillCommandOp.ToggleAuto, 0, 0, 1f));
        Assert.IsTrue(_graph.AutoMode);
        Assert.AreEqual(version + 1, _graph.StateVersion);

        Assert.IsTrue(_graph.TryTakeTopology(out _));
        Assert.IsFalse(_graph.TryTakeTopology(out _), "skipped when unchanged");
        Assert.IsTrue(_graph.TryExecute(SkillCommandOp.Sync, 0, 0, 1f));
        Assert.IsTrue(_graph.TryTakeTopology(out _), "sync forces a resend");
    }

    [Test]
    public void TopologyAndLevelsReplicateToAMirror()
    {
        FreePoints();
        Assert.IsTrue(FindBuildable(out int u, out int v));
        Assert.IsTrue(_graph.TryExecute(SkillCommandOp.Build, u, v, 1f));
        Assert.IsTrue(_graph.TryExecute(SkillCommandOp.ToggleAuto, 0, 0, 1f));
        for (int k = 0; k < 60; k++) _graph.Award(EntityAction.MoveTo, k);
        _graph.Store.SetLevel(PlayerSkillGraph.Slot, u, 12.345f, 2f);

        Assert.IsTrue(_graph.TryTakeLevels(0f, out var firstLevels));
        Assert.IsTrue(_mirror.IsStale(in firstLevels), "levels before topology are stale");
        Assert.IsFalse(_mirror.ApplyLevels(in firstLevels, 1f));

        Assert.IsTrue(_graph.TryTakeTopology(out var topology));
        Assert.IsTrue(_mirror.ApplyTopology(in topology));
        Assert.AreEqual(_graph.Topology.Revision, _mirror.Topology.Revision);
        Assert.AreEqual(_graph.AutoMode, _mirror.AutoMode);
        for (int a = 0; a < _atlas.Count; a++) Assert.AreEqual(_graph.Topology.ActiveRow(a), _mirror.Topology.ActiveRow(a));
        Assert.AreEqual(_graph.Topology.IslandMask, _mirror.Topology.IslandMask);
        Assert.AreEqual(_graph.Topology.PhiPeak, _mirror.Topology.PhiPeak);

        _graph.Store.SetLevel(PlayerSkillGraph.Slot, u, 13.5f, 3f);
        Assert.IsTrue(_graph.TryTakeLevels(10f, out var levels));
        Assert.IsFalse(_mirror.IsStale(in levels));
        Assert.IsTrue(_mirror.ApplyLevels(in levels, 2f));
        float tolerance = 1f / _rules.SkillLevelFixedScale;
        for (int a = 0; a < _atlas.Count; a++)
        {
            Assert.AreEqual(_graph.Store.Level(PlayerSkillGraph.Slot, a), _mirror.Store.Level(PlayerSkillGraph.Slot, a), tolerance, "level " + a);
            Assert.AreEqual(_graph.Store.Peak(PlayerSkillGraph.Slot, a), _mirror.Store.Peak(PlayerSkillGraph.Slot, a), tolerance, "peak " + a);
        }
        Assert.AreEqual(_graph.Store.Root(PlayerSkillGraph.Slot), _mirror.Store.Root(PlayerSkillGraph.Slot));
    }

    [Test]
    public void UnchangedLevelsAreNotSentAgain()
    {
        Assert.IsTrue(_graph.TryTakeLevels(0f, out _));
        Assert.IsFalse(_graph.TryTakeLevels(0.5f, out _), "interval");
        Assert.IsFalse(_graph.TryTakeLevels(5f, out _), "unchanged");
        _graph.Store.SetLevel(PlayerSkillGraph.Slot, 1, 20f, 5f);
        Assert.IsTrue(_graph.TryTakeLevels(10f, out _));
    }

    [Test]
    public void MaliciousTopologyPacketsAreRefusedWithoutChangingTheMirror()
    {
        Assert.IsTrue(_graph.TryTakeTopology(out var good));
        Assert.IsTrue(_mirror.ApplyTopology(in good));
        int revision = _mirror.Topology.Revision;
        ulong row0 = _mirror.Topology.ActiveRow(0);

        var bad = Copy(good);
        bad.Count = (byte)(_atlas.Count + 1);
        Assert.IsFalse(_mirror.ApplyTopology(in bad));

        bad = Copy(good);
        bad.PhiPeak = float.NaN;
        Assert.IsFalse(_mirror.ApplyTopology(in bad));

        bad = Copy(good);
        bad.PhiPeak = float.PositiveInfinity;
        Assert.IsFalse(_mirror.ApplyTopology(in bad));

        bad = Copy(good);
        bad.Rows[0] |= 1ul;
        Assert.IsFalse(_mirror.ApplyTopology(in bad), "self loop");

        bad = Copy(good);
        bad.Rows[0] = ulong.MaxValue;
        Assert.IsFalse(_mirror.ApplyTopology(in bad), "outside the potential frame");

        bad = Copy(good);
        bad.PrunedMask = ulong.MaxValue;
        Assert.IsFalse(_mirror.ApplyTopology(in bad), "mask beyond the count");

        bad = Copy(good);
        bad.BridgeCount = 200;
        Assert.IsFalse(_mirror.ApplyTopology(in bad));

        bad = Copy(good);
        bad.BridgeCount = 1;
        bad.Bridges[0] = (ushort)((250 << 8) | 251);
        Assert.IsFalse(_mirror.ApplyTopology(in bad));

        bad = Copy(good);
        bad.Forest = new float4(float.NaN, 0f, 0f, 0f);
        Assert.IsFalse(_mirror.ApplyTopology(in bad));

        bad = Copy(good);
        bad.Rows = null;
        Assert.IsFalse(_mirror.ApplyTopology(in bad));

        Assert.AreEqual(revision, _mirror.Topology.Revision);
        Assert.AreEqual(row0, _mirror.Topology.ActiveRow(0));
    }

    [Test]
    public void AsymmetricRowsAreRefused()
    {
        Assert.IsTrue(_graph.TryTakeTopology(out var good));
        int u = -1, v = -1;
        for (int a = 0; a < _atlas.Count && u < 0; a++)
        for (int b = a + 1; b < _atlas.Count; b++)
            if (_atlas.HasPotentialEdge(a, b) && !_graph.Topology.IsActive(a, b)) { u = a; v = b; break; }
        Assert.GreaterOrEqual(u, 0);
        var bad = Copy(good);
        bad.Rows[u] |= 1ul << v;
        Assert.IsFalse(_mirror.ApplyTopology(in bad));
    }

    [Test]
    public void MalformedLevelPacketsAreRefused()
    {
        Assert.IsTrue(_graph.TryTakeTopology(out var topology));
        Assert.IsTrue(_mirror.ApplyTopology(in topology));
        Assert.IsTrue(_graph.TryTakeLevels(0f, out var good));

        var bad = good;
        bad.Root = (byte)_atlas.Count;
        Assert.IsFalse(_mirror.ApplyLevels(in bad, 1f));

        bad = good;
        bad.Count = (byte)(_atlas.Count + 3);
        Assert.IsFalse(_mirror.ApplyLevels(in bad, 1f));

        bad = good;
        bad.Levels = new ushort[2];
        Assert.IsFalse(_mirror.ApplyLevels(in bad, 1f));

        bad = good;
        bad.Peaks = null;
        Assert.IsFalse(_mirror.ApplyLevels(in bad, 1f));
    }

    [Test]
    public void FixedPointKeepsTheRangeAndSaturates()
    {
        Assert.AreEqual(0, SkillLevelsPacket.Quantize(-5f, 100f));
        Assert.AreEqual(0, SkillLevelsPacket.Quantize(float.NaN, 100f));
        Assert.AreEqual(ushort.MaxValue, SkillLevelsPacket.Quantize(1e9f, 100f));
        Assert.AreEqual(1234, SkillLevelsPacket.Quantize(12.34f, 100f));
        Assert.AreEqual(12.34f, SkillLevelsPacket.Dequantize(1234, 100f), 1e-4f);
    }

    [Test]
    public void SaveRoundTripsGenesTopologyAndLevels()
    {
        FreePoints();
        Assert.IsTrue(FindBuildable(out int u, out int v));
        Assert.IsTrue(_graph.TryExecute(SkillCommandOp.Build, u, v, 1f));
        _graph.Store.SetLevel(PlayerSkillGraph.Slot, 3, 21.5f, 1f);
        var save = new PlayerSkillSave();
        _graph.Capture(save, 1f);

        var genes = GeneticParameters.Default;
        using var restored = new Holder(PlayerSkillGraph.Create(_atlas, ref genes, save.Clone(), true));
        var g = restored.Graph;

        Assert.AreEqual(_graph.Genes.SkillOriginX, genes.SkillOriginX);
        Assert.AreEqual(_graph.Genes.PlastNeed, genes.PlastNeed);
        for (int a = 0; a < _atlas.Count; a++) Assert.AreEqual(_graph.Topology.ActiveRow(a), g.Topology.ActiveRow(a));
        Assert.AreEqual(_graph.Topology.SpentEdgePoints, g.Topology.SpentEdgePoints);
        Assert.AreEqual(_graph.Topology.IslandMask, g.Topology.IslandMask);
        Assert.AreEqual(21.5f, g.Store.Level(PlayerSkillGraph.Slot, 3), 0.01f);
        Assert.AreEqual(_graph.Store.Root(PlayerSkillGraph.Slot), g.Store.Root(PlayerSkillGraph.Slot));
    }

    [Test]
    public void MalformedSavesStartAFreshGraph()
    {
        var good = new PlayerSkillSave();
        _graph.Capture(good, 1f);
        Assert.IsTrue(PlayerSkillGraph.IsWellFormed(good, _atlas.Count));

        var bad = good.Clone();
        bad.skillCount = _atlas.Count + 1;
        Assert.IsFalse(PlayerSkillGraph.IsWellFormed(bad, _atlas.Count));

        bad = good.Clone();
        bad.levels[0] = float.NaN;
        Assert.IsFalse(PlayerSkillGraph.IsWellFormed(bad, _atlas.Count));

        bad = good.Clone();
        bad.genes[0] = 5f;
        Assert.IsFalse(PlayerSkillGraph.IsWellFormed(bad, _atlas.Count));

        bad = good.Clone();
        bad.active = new ulong[3];
        Assert.IsFalse(PlayerSkillGraph.IsWellFormed(bad, _atlas.Count));

        bad = good.Clone();
        bad.phiPeak = float.PositiveInfinity;
        Assert.IsFalse(PlayerSkillGraph.IsWellFormed(bad, _atlas.Count));
        Assert.IsFalse(PlayerSkillGraph.IsWellFormed(null, _atlas.Count));

        bad = good.Clone();
        bad.active[0] = ulong.MaxValue;
        var genes = GeneticParameters.Default;
        using var fresh = new Holder(PlayerSkillGraph.Create(_atlas, ref genes, bad, true));
        Assert.IsNotNull(fresh.Graph.Topology);
        Assert.AreEqual(1, fresh.Graph.StateVersion);
    }

    [Test]
    public void OldSavesWithoutSkillsLoadAFreshGraph()
    {
        var state = new WorldState();
        Assert.IsNull(state.playerSkills);
        var genes = GeneticParameters.Default;
        using var fresh = new Holder(PlayerSkillGraph.Create(_atlas, ref genes, state.playerSkills, true));
        Assert.IsNotNull(fresh.Graph.Topology);
        Assert.IsNull(state.Clone().playerSkills);

        _graph.Capture(state.playerSkills = new PlayerSkillSave(), 1f);
        Assert.IsNotNull(state.Clone().playerSkills);
        Assert.AreEqual(_atlas.Count, state.Clone().playerSkills.skillCount);
    }

    [Test]
    public void EffectsFollowTheLevelsThroughTheSameModifiers()
    {
        using var handle = new Holder(null, SkillGraphTestAtlas.BuildGrid(SkillChannel.EnergyCapacity, false));
        var genes = GeneticParameters.Default;
        var graph = PlayerSkillGraph.Create(handle.Atlas, ref genes, null, true);
        try
        {
            graph.Refresh();
            Assert.AreEqual(1f, graph.Modifiers.EnergyCapacity);
            graph.Store.SetLevel(PlayerSkillGraph.Slot, graph.Store.Root(PlayerSkillGraph.Slot), 40f, 1f);
            graph.Refresh();
            Assert.IsTrue(graph.ConsumeModifiersChanged());
            Assert.Greater(graph.Modifiers.EnergyCapacity, 1f);
            Assert.IsFalse(graph.ConsumeModifiersChanged());
        }
        finally
        {
            graph.Dispose();
        }
    }

    [Test]
    public void TickSweepsAndSolvesInAutoModeOnTheSameRulesAsAgents()
    {
        for (int v = 0; v < _atlas.Count; v++) _graph.Store.SetLevel(PlayerSkillGraph.Slot, v, 30f, 0f);
        Assert.IsTrue(_graph.TryExecute(SkillCommandOp.ToggleAuto, 0, 0, 0f));
        int revision = _graph.Topology.Revision;
        _graph.Tick(1000f);
        Assert.Greater(_graph.Topology.Revision, revision);
        Assert.Less(_graph.Store.Level(PlayerSkillGraph.Slot, 0), 30f, "decayed by the sweep");
        Assert.GreaterOrEqual(_graph.Store.Level(PlayerSkillGraph.Slot, 0), _rules.SkillSavingsFloor * 30f - 1e-3f);
    }

    [Test]
    public void AwardNeverAllocates()
    {
        float now = 0f;
        AllocationAssert.Zero(() =>
        {
            now += 1f;
            _graph.Award(EntityAction.Take, now);
        }, 200, "PlayerSkillGraph.Award");
    }

    private static SkillTopologyPacket Copy(in SkillTopologyPacket p)
    {
        var c = p;
        c.Rows = (ulong[])p.Rows.Clone();
        c.Bridges = (ushort[])p.Bridges.Clone();
        return c;
    }

    private sealed class Holder : System.IDisposable
    {
        public readonly PlayerSkillGraph Graph;
        public readonly SkillAtlas Atlas;
        public Holder(PlayerSkillGraph graph, SkillAtlas atlas = null)
        {
            Graph = graph;
            Atlas = atlas;
        }
        public void Dispose()
        {
            Graph?.Dispose();
            if (Atlas != null) Object.DestroyImmediate(Atlas);
        }
    }
}
