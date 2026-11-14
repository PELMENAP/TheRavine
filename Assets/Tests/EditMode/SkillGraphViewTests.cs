using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public class SkillGraphViewTests
{
    private sealed class Source : ISkillGraphSource
    {
        public SkillAtlas Atlas { get; set; }
        public SkillGraphTopology Topology { get; set; }
        public SkillGraphStore Store { get; set; }
        public int StoreIndex => 0;
        public int StateVersion { get; set; }
        public bool AutoMode { get; set; }
    }

    private SimulationRules _rules;
    private SkillAtlas _atlas;
    private SkillGraphTopology _topology;
    private SkillGraphStore _store;
    private Source _source;
    private readonly SkillGraphSnapshot _snapshot = new();

    [SetUp]
    public void SetUp()
    {
        _rules = TestRules.Bind();
        _atlas = SkillAtlas.BuildDefault();
        _topology = SkillGraphTopology.Create(_atlas, default);
        _store = new SkillGraphStore(1, _atlas.Count);
        _store.Reset(0, _atlas.PositionOf(0), _topology, _atlas);
        _store.SetColony(0, 0);
        _store.PublishColony(0, _topology);
        _source = new Source { Atlas = _atlas, Topology = _topology, Store = _store };
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
        _topology.Dispose();
        Object.DestroyImmediate(_atlas);
        TestRules.Release(_rules);
    }

    private HashSet<int> Written()
    {
        var ids = new HashSet<int>();
        for (int i = 0; i < _snapshot.VertexCount; i++) ids.Add(_snapshot.Skill[i]);
        return ids;
    }

    private SkillVertexState StateOf(int skill) => _snapshot.State[_snapshot.SlotOf(skill)];

    [Test]
    public void FreshGraphShowsTheRootItsNeighboursAndNothingElse()
    {
        SkillGraphQuery.Fill(_source, _snapshot);
        int root = _store.Root(0);

        var expected = new HashSet<int> { root };
        for (int v = 0; v < _atlas.Count; v++)
            if (_atlas.HasPotentialEdge(root, v)) expected.Add(v);
        CollectionAssert.AreEquivalent(expected, Written());

        Assert.AreEqual(SkillVertexState.Open, StateOf(root));
        for (int v = 0; v < _atlas.Count; v++)
        {
            if (v == root || !_atlas.HasPotentialEdge(root, v)) continue;
            if (_topology.IsActive(root, v)) Assert.AreEqual(SkillVertexState.Available, StateOf(v), "active neighbour " + v);
            else
            {
                bool island = ((_topology.IslandMask >> v) & 1ul) != 0ul;
                Assert.AreEqual(island ? SkillVertexState.Island : SkillVertexState.Silhouette, StateOf(v), "potential neighbour " + v);
            }
        }
        for (int i = 0; i < _snapshot.VertexCount; i++) Assert.AreNotEqual(SkillVertexState.Hidden, _snapshot.State[i]);

        for (int e = 0; e < _snapshot.EdgeCount; e++)
        {
            int a = _snapshot.EdgeFrom[e], b = _snapshot.EdgeTo[e];
            Assert.IsTrue(a == root || b == root, "edge " + a + "-" + b + " does not touch the root");
            Assert.IsTrue(_atlas.HasPotentialEdge(a, b));
            bool active = _topology.IsActive(a, b);
            Assert.AreEqual(active, _snapshot.EdgeKind[e] != SkillEdgeKind.Potential);
        }
        int rootEdges = 0;
        for (int v = 0; v < _atlas.Count; v++) if (_atlas.HasPotentialEdge(root, v)) rootEdges++;
        Assert.AreEqual(rootEdges, _snapshot.EdgeCount);
    }

    [Test]
    public void OpeningAVertexRevealsItsNeighbours()
    {
        SkillGraphQuery.Fill(_source, _snapshot);
        int root = _store.Root(0);
        int neighbour = -1;
        for (int v = 0; v < _atlas.Count && neighbour < 0; v++)
            if (_topology.IsActive(root, v)) neighbour = v;
        Assert.GreaterOrEqual(neighbour, 0);
        int before = _snapshot.VertexCount;

        _store.SetLevel(0, neighbour, 3f, 0f);
        SkillGraphQuery.Fill(_source, _snapshot);

        Assert.AreEqual(SkillVertexState.Open, StateOf(neighbour));
        var expected = new HashSet<int> { root, neighbour };
        for (int v = 0; v < _atlas.Count; v++)
            if (_atlas.HasPotentialEdge(root, v) || _atlas.HasPotentialEdge(neighbour, v)) expected.Add(v);
        CollectionAssert.AreEquivalent(expected, Written());
        Assert.GreaterOrEqual(_snapshot.VertexCount, before);
        Assert.AreEqual(3, _snapshot.Level[_snapshot.SlotOf(neighbour)]);
    }

    [Test]
    public void LevelDataIsFilledForVisibleVerticesOnly()
    {
        int root = _store.Root(0);
        _store.SetLevel(0, root, 7.4f, 0f);
        SkillGraphQuery.Fill(_source, _snapshot);
        int slot = _snapshot.SlotOf(root);

        Assert.AreEqual(7, _snapshot.Level[slot]);
        Assert.AreEqual(0.4f, _snapshot.Fraction[slot], 1e-4f);
        Assert.AreEqual(7.4f, _snapshot.Peak[slot], 1e-4f);
        Assert.AreEqual(_rules.SkillSavingsFloor * 7.4f, _snapshot.Floor[slot], 1e-4f);
        Assert.GreaterOrEqual(_snapshot.Talent[slot], _rules.SkillTalentMin - 1e-6f);
        Assert.AreEqual(1f, _snapshot.Decay[slot]);
        Assert.GreaterOrEqual(_snapshot.LogicBoost[slot], 1f);
        Assert.AreEqual(1f, _snapshot.FocusBoost);
    }

    [Test]
    public void BudgetAndRevisionComeFromTheTopology()
    {
        _topology.UpdatePhi(1000f);
        _source.StateVersion = 5;
        _source.AutoMode = true;
        SkillGraphQuery.Fill(_source, _snapshot);

        Assert.AreEqual(_topology.Earned(in SimulationRules.Frame), _snapshot.Earned);
        Assert.AreEqual(_snapshot.Earned - _topology.SpentEdgePoints, _snapshot.Available);
        Assert.AreEqual(_topology.Revision, _snapshot.Revision);
        Assert.AreEqual(5, _snapshot.StateVersion);
        Assert.IsTrue(_snapshot.AutoMode);
        Assert.AreEqual(1000f, _snapshot.PhiPeak);
    }

    [Test]
    public void ChecksumChangesWithLevelsAndRevision()
    {
        int a = SkillGraphQuery.Checksum(_source);
        _store.SetLevel(0, 2, 5f, 0f);
        int b = SkillGraphQuery.Checksum(_source);
        Assert.AreNotEqual(a, b);
        _source.StateVersion++;
        Assert.AreNotEqual(b, SkillGraphQuery.Checksum(_source));
    }

    [Test]
    public void PrunedBranchIsShownAsPrunedNextToItsOpenNeighbour()
    {
        _topology.UpdatePhi(1000f);
        int root = _store.Root(0);
        int near = -1, far = -1;
        for (int u = 0; u < _atlas.Count && near < 0; u++)
        for (int v = 0; v < _atlas.Count; v++)
            if (u == root && _topology.IsActive(u, v) && _topology.CanPrune(u, v, root)) { near = u; far = v; break; }
        if (near < 0) Assert.Ignore("the root has no prunable bridge in this atlas");

        Assert.IsTrue(_topology.TryPrune(near, far, root));
        SkillGraphQuery.Fill(_source, _snapshot);

        Assert.AreEqual(SkillVertexState.Pruned, StateOf(far));
        bool edge = false;
        for (int e = 0; e < _snapshot.EdgeCount; e++)
            edge |= _snapshot.EdgeKind[e] == SkillEdgeKind.Potential && (_snapshot.EdgeFrom[e] == far || _snapshot.EdgeTo[e] == far);
        Assert.IsTrue(edge, "the pruned bridge is a restore handle");
    }

    [Test]
    public void FillDoesNotAllocate()
    {
        _store.SetLevel(0, _store.Root(0), 4f, 0f);
        SkillGraphQuery.Fill(_source, _snapshot);
        AllocationAssert.Zero(() => SkillGraphQuery.Fill(_source, _snapshot), 100, "SkillGraphQuery.Fill");
        AllocationAssert.Zero(() => SkillGraphQuery.Checksum(_source), 100, "SkillGraphQuery.Checksum");
    }

    [Test]
    public void CanvasBuildsOneNodePerVisibleVertexWithStateClasses()
    {
        var snapshot = new SkillGraphSnapshot();
        snapshot.Clear();
        var states = new[] { SkillVertexState.Open, SkillVertexState.Available, SkillVertexState.Silhouette, SkillVertexState.Island, SkillVertexState.Pruned };
        for (int i = 0; i < states.Length; i++)
        {
            int slot = snapshot.AddVertex(i * 2);
            snapshot.State[slot] = states[i];
            snapshot.Position[slot] = new Vector2(0.1f * (i + 1), 0.5f);
            snapshot.Level[slot] = i;
            snapshot.Clique[slot] = (byte)(i == 0 ? 3 : 1);
            snapshot.Decay[slot] = 1f;
        }
        snapshot.AddEdge(0, 2, SkillEdgeKind.Active);
        snapshot.AddEdge(2, 4, SkillEdgeKind.Bridge);

        var canvas = new SkillGraphCanvas();
        canvas.Bind(snapshot);

        Assert.AreEqual(5, canvas.childCount);
        string[] classes = { SkillNodeElement.ClassOpen, SkillNodeElement.ClassAvailable, SkillNodeElement.ClassSilhouette, SkillNodeElement.ClassIsland, SkillNodeElement.ClassPruned };
        for (int i = 0; i < 5; i++)
        {
            var node = (SkillNodeElement)canvas[i];
            Assert.IsTrue(node.ClassListContains(classes[i]), "node " + i);
            Assert.AreEqual(i * 2, node.Skill);
            for (int c = 0; c < 5; c++) if (c != i) Assert.IsFalse(node.ClassListContains(classes[c]));
        }

        snapshot.Clear();
        snapshot.AddVertex(1);
        snapshot.State[0] = SkillVertexState.Open;
        canvas.Bind(snapshot);
        Assert.AreEqual(5, canvas.childCount, "nodes are pooled");
        Assert.AreEqual(DisplayStyle.Flex, canvas[0].style.display.value);
        Assert.AreEqual(DisplayStyle.None, canvas[1].style.display.value);
    }

    [Test]
    public void GeometryPicksTheNearestSegmentWithinTolerance()
    {
        Assert.AreEqual(5f, SkillGraphGeometry.DistanceToSegment(new Vector2(5f, 5f), new Vector2(0f, 0f), new Vector2(10f, 0f)), 1e-5f);
        Assert.AreEqual(5f, SkillGraphGeometry.DistanceToSegment(new Vector2(15f, 0f), new Vector2(0f, 0f), new Vector2(10f, 0f)), 1e-5f);
        Assert.AreEqual(5f, SkillGraphGeometry.DistanceToSegment(new Vector2(3f, 4f), new Vector2(0f, 0f), new Vector2(0f, 0f)), 1e-5f);

        var snapshot = new SkillGraphSnapshot();
        snapshot.Clear();
        snapshot.AddVertex(0);
        snapshot.AddVertex(1);
        snapshot.AddVertex(2);
        snapshot.AddEdge(0, 1, SkillEdgeKind.Active);
        snapshot.AddEdge(1, 2, SkillEdgeKind.Potential);
        var points = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(100f, 100f) };

        Assert.AreEqual(0, SkillGraphGeometry.NearestEdge(snapshot, slot => points[slot], new Vector2(50f, 3f), 8f));
        Assert.AreEqual(1, SkillGraphGeometry.NearestEdge(snapshot, slot => points[slot], new Vector2(103f, 50f), 8f));
        Assert.AreEqual(-1, SkillGraphGeometry.NearestEdge(snapshot, slot => points[slot], new Vector2(50f, 50f), 8f));
    }

    [Test]
    public void CanvasMapsUnitPointsOntoThePickedEdge()
    {
        var snapshot = new SkillGraphSnapshot();
        snapshot.Clear();
        snapshot.AddVertex(4);
        snapshot.AddVertex(9);
        snapshot.Position[0] = new Vector2(0.3f, 0.5f);
        snapshot.Position[1] = new Vector2(0.7f, 0.5f);
        snapshot.State[0] = SkillVertexState.Open;
        snapshot.State[1] = SkillVertexState.Silhouette;
        snapshot.AddEdge(4, 9, SkillEdgeKind.Potential);

        var canvas = new SkillGraphCanvas();
        canvas.Bind(snapshot);
        Vector2 mid = canvas.UnitToLocal(new Vector2(0.5f, 0.5f));
        Assert.AreEqual(0, SkillGraphGeometry.NearestEdge(snapshot, slot => canvas.UnitToLocal(snapshot.Position[slot]), mid, SkillGraphPalette.PickTolerance));
    }
}
