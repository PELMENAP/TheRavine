using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class SkillGraphAgentTests
{
    private SimulationRules _rules;
    private SkillAtlas _atlas;
    private SkillGraphTopology _topology;
    private SkillGraphStore _store;

    [SetUp]
    public void SetUp()
    {
        _rules = TestRules.Bind();
        _atlas = SkillAtlas.BuildDefault();
        _topology = SkillGraphTopology.Create(_atlas, default);
        _store = new SkillGraphStore(4, _atlas.Count);
    }

    [TearDown]
    public void TearDown()
    {
        _store.Dispose();
        _topology.Dispose();
        Object.DestroyImmediate(_atlas);
        TestRules.Release(_rules);
    }

    private static GeneticParameters Genes()
    {
        var g = GeneticParameters.Default;
        g.SkillOriginX = 0.5f;
        g.SkillOriginY = 0.5f;
        return g;
    }

    [Test]
    public void SkillGenesHaveDefaultsAndRanges()
    {
        var g = GeneticParameters.Default;
        Assert.AreEqual(0.5f, g.SkillOriginX);
        Assert.AreEqual(0.5f, g.SkillOriginY);
        Assert.AreEqual(1f, g.PlastHebb);
        Assert.AreEqual(1f, g.PlastStruct);
        Assert.AreEqual(1f, g.PlastExplore);
        Assert.AreEqual(1f, g.PlastNeed);
        Assert.AreEqual(GeneticParameters.IdxPlastNeed + 1, GeneticParameters.IdxPredisposition0);
        Assert.AreEqual(GeneticParameters.IdxPredisposition0 + GeneticParameters.PredispositionCount, GeneticParameters.GeneCount);
        Assert.AreEqual(GeneticParameters.GeneCount, GeneticParameters.ParameterRanges.Length);

        var origin = GeneticParameters.ParameterRanges[GeneticParameters.IdxSkillOriginX];
        var plast = GeneticParameters.ParameterRanges[GeneticParameters.IdxPlastHebb];
        Assert.AreEqual((0f, 1f), (origin.min, origin.max));
        Assert.AreEqual((0f, 2f), (plast.min, plast.max));
    }

    [Test]
    public void SkillGenesStayInRangeAndAreIndividualTraits()
    {
        var a = GeneticParameters.Default;
        var b = GeneticParameters.Default;
        a.MutationChance = 0.7f;
        b.MutationChance = 0.7f;
        for (int i = 0; i < 200; i++)
        {
            GeneticParameters.Crossover(in a, in b, out var child);
            Assert.That(child.SkillOriginX, Is.InRange(0f, 1f));
            Assert.That(child.SkillOriginY, Is.InRange(0f, 1f));
            Assert.That(child.PlastHebb, Is.InRange(0f, 2f));
            Assert.That(child.PlastNeed, Is.InRange(0f, 2f));
            a = child;
        }
        Assert.IsTrue(GeneticParameters.IsPhenotypic(GeneticParameters.IdxSkillOriginX));
        Assert.IsTrue(GeneticParameters.IsPhenotypic(GeneticParameters.IdxPlastNeed));

        var own = GeneticParameters.Default;
        var donor = GeneticParameters.Default;
        donor.SkillOriginX = 0.1f;
        donor.PlastHebb = 0.2f;
        GeneticParameters.CopyLearningGenes(in donor, ref own);
        Assert.AreEqual(0.5f, own.SkillOriginX);
        Assert.AreEqual(1f, own.PlastHebb);
    }

    [Test]
    public void ResetReadsOriginAndPlasticityFromTheGenes()
    {
        var g = Genes();
        g.SkillOriginX = _atlas.PositionOf(7).x;
        g.SkillOriginY = _atlas.PositionOf(7).y;
        g.PlastExplore = 1.5f;
        _store.Reset(0, in g, _topology, _atlas);
        _store.SetColony(0, 2);

        Assert.AreEqual(1.5f, _store.PlasticityOf(0).z);
        Assert.AreEqual(2, _store.ColonyOf(0));
        Assert.IsFalse(_store.IsDirty(0));
        int expected = ((_topology.IslandMask >> 7) & 1ul) == 0ul ? 7 : _store.Root(0);
        Assert.AreEqual(expected, _store.Root(0));
    }

    [Test]
    public void RetargetKeepsLevelsAndPeaksButMovesRootAndTalent()
    {
        var g = Genes();
        _store.Reset(0, in g, _topology, _atlas);
        _store.SetLevel(0, 3, 12f, 1f);
        var talentBefore = new float[_atlas.Count];
        for (int v = 0; v < talentBefore.Length; v++) talentBefore[v] = _store.Talent(0, v);

        g.SkillOriginX = 0f;
        g.SkillOriginY = 0f;
        _store.Retarget(0, in g, _topology, _atlas);

        Assert.AreEqual(12f, _store.Level(0, 3));
        Assert.AreEqual(12f, _store.Peak(0, 3));
        Assert.AreEqual(SkillGraphMath.RootVertex(float2.zero, _topology.IslandMask, _atlas.NativePositions), _store.Root(0));
        bool moved = false;
        for (int v = 0; v < talentBefore.Length; v++) moved |= !Mathf.Approximately(talentBefore[v], _store.Talent(0, v));
        Assert.IsTrue(moved);
        for (int v = 0; v < talentBefore.Length; v++) Assert.GreaterOrEqual(_store.Talent(0, v), SimulationRules.Frame.SkillTalentMin - 1e-6f);
    }

    [Test]
    public void ColonyTopologyIsCreatedWithTheColony()
    {
        var registry = new ColonyRegistry();
        try
        {
            var colony = registry.Add(float2.zero, null, 8);
            var wild = registry.Add(float2.zero, null, 8, true);
            Assert.IsNotNull(colony.SkillTopology);
            Assert.IsNotNull(wild.SkillTopology);
            Assert.IsTrue(colony.SkillTopology.IsCreated);
            Assert.AreEqual(SkillAtlas.Active.Count, colony.SkillTopology.Count);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [Test]
    public void StrikeLevelRaisesAttackDamageAndNothingElse()
    {
        using var handle = new AtlasHandle(SkillGraphTestAtlas.BuildGrid(SkillChannel.Attack, false));
        var atlas = handle.Atlas;
        using var topology = new TopologyHandle(SkillGraphTopology.Create(atlas, default));
        var store = new SkillGraphStore(1, atlas.Count);
        try
        {
            store.Reset(0, atlas.PositionOf(0), topology.Topology, atlas);
            int root = store.Root(0);

            var tuning = new EntityTuning
            {
                MaxHealth = 100f, MaxEnergy = 80f, EnergyRegenRate = 1f, MoveSpeed = 3f, RunSpeed = 6f,
                EnergyCostMoving = 0.1f, EnergyCostRunning = 0.3f, DetectionRadius = 12f, AttackRange = 2f,
                AttackDamage = 10f, AttackCooldown = 1f, AttackEnergyCost = 1f, ReproduceEnergyCost = 20f, ReproduceHealthCost = 5f,
            };
            var genes = Genes();
            var role = CasteModifiers.Neutral;

            var before = SkillModifiers.From(store, 0, topology.Topology, atlas);
            var plain = EntityTuning.Express(in tuning, in genes, in role);
            var neutral = EntityTuning.Express(in tuning, in genes, in role, in before);
            Assert.AreEqual(plain.AttackDamage, neutral.AttackDamage);
            Assert.AreEqual(plain.MoveSpeed, neutral.MoveSpeed);
            Assert.AreEqual(plain.EnergyCostMoving, neutral.EnergyCostMoving);

            store.SetLevel(0, root, 40f, 1f);
            var after = SkillModifiers.From(store, 0, topology.Topology, atlas);
            var raised = EntityTuning.Express(in tuning, in genes, in role, in after);

            Assert.Greater(raised.AttackDamage, neutral.AttackDamage);
            Assert.AreEqual(neutral.MaxHealth, raised.MaxHealth);
            Assert.AreEqual(neutral.MaxEnergy, raised.MaxEnergy);
            Assert.AreEqual(neutral.MoveSpeed, raised.MoveSpeed);
            Assert.AreEqual(neutral.RunSpeed, raised.RunSpeed);
            Assert.AreEqual(neutral.EnergyCostMoving, raised.EnergyCostMoving);
            Assert.AreEqual(neutral.AttackCooldown, raised.AttackCooldown);
            Assert.AreEqual(neutral.DetectionRadius, raised.DetectionRadius);
            Assert.AreEqual(neutral.ReproduceEnergyCost, raised.ReproduceEnergyCost);
            Assert.IsTrue(store.IsDirty(0));
        }
        finally
        {
            store.Dispose();
        }
    }

    [Test]
    public void DividerChannelsLowerCosts()
    {
        var skills = SkillModifiers.Neutral;
        skills.MoveEnergyCost = 0.5f;
        skills.ReproduceCost = 0.25f;
        var tuning = new EntityTuning { MaxHealth = 1f, MaxEnergy = 1f, MoveSpeed = 1f, RunSpeed = 1f, EnergyCostMoving = 2f, EnergyCostRunning = 4f, ReproduceEnergyCost = 8f, AttackCooldown = 1f };
        var genes = Genes();
        var role = CasteModifiers.Neutral;
        var plain = EntityTuning.Express(in tuning, in genes, in role);
        var cheap = EntityTuning.Express(in tuning, in genes, in role, in skills);
        Assert.AreEqual(plain.EnergyCostMoving * 0.5f, cheap.EnergyCostMoving, 1e-5f);
        Assert.AreEqual(plain.EnergyCostRunning * 0.5f, cheap.EnergyCostRunning, 1e-5f);
        Assert.AreEqual(plain.ReproduceEnergyCost * 0.25f, cheap.ReproduceEnergyCost, 1e-5f);
    }

    [Test]
    public void CombineMultipliesTheSharedChannels()
    {
        var role = CasteModifiers.Neutral;
        role.Attack = 2f;
        var skills = SkillModifiers.Neutral;
        skills.Attack = 1.5f;
        skills.Feed = 3f;
        var combined = CasteModifiers.Combine(in role, in skills);
        Assert.AreEqual(3f, combined.Attack);
        Assert.AreEqual(3f, combined.Feed);
        Assert.AreEqual(role.Fear, combined.Fear);
    }

    [Test]
    public void DecayJobMatchesTheReferenceAndMarksIntegerChanges()
    {
        int n = _atlas.Count;
        for (int i = 0; i < 3; i++)
        {
            _store.Reset(i, GeneticParameters.Default, _topology, _atlas);
            _store.SetColony(i, 0);
            for (int v = 0; v < n; v++) _store.SetLevel(i, v, 40f, 0f);
            _store.ClearDirty(i);
        }
        var cycle = _store.ColonyCycle;
        for (int v = 0; v < n; v++) cycle[v] = 0.5f;
        var prunedRows = _store.ColonyPruned;
        prunedRows[0] = 1ul << 2;

        ref readonly var f = ref SimulationRules.Frame;
        var job = new SkillDecayJob
        {
            Offset = 2, Count = 3, SkillCount = n, Now = 600f, DecayRate = f.SkillDecayRate, SavingsFloor = f.SkillSavingsFloor,
            Levels = _store.Levels, LastUsed = _store.LastUsed, Dirty = _store.Dirty, Peaks = _store.Peaks,
            Colonies = _store.Colonies, ColonyCycle = _store.ColonyCycle, ColonyPruned = _store.ColonyPruned,
        };
        job.Execute(0);
        job.Execute(1);

        float expected = SkillGraphMath.Decay(40f, 40f, 600f, 0.5f, in f);
        foreach (int agent in new[] { 2, 0 })
        {
            Assert.AreEqual(expected, _store.Level(agent, 0), 1e-3f);
            Assert.AreEqual(40f, _store.Level(agent, 2), "pruned skill is frozen");
            Assert.AreEqual(40f, _store.Peak(agent, 0));
            Assert.GreaterOrEqual(_store.Level(agent, 0), f.SkillSavingsFloor * 40f);
            Assert.IsTrue(_store.IsDirty(agent));
        }
        Assert.AreEqual(40f, _store.Level(1, 0));
        Assert.IsFalse(_store.IsDirty(1));
    }

    [Test]
    public void DecayJobVisitsEveryAgentOncePerSlidingWindow()
    {
        int n = _atlas.Count;
        for (int i = 0; i < 4; i++)
        {
            _store.Reset(i, GeneticParameters.Default, _topology, _atlas);
            _store.SetLevel(i, 0, 40f, 0f);
        }
        var cycle = _store.ColonyCycle;
        for (int v = 0; v < n; v++) cycle[v] = 1f;

        int cursor = 3;
        for (int window = 0; window < 4; window++)
        {
            _store.ScheduleDecay(cursor, 1, 4, 100f * (window + 1), 0.001f, 0.25f).Complete();
            cursor = (cursor + 1) % 4;
        }
        for (int i = 0; i < 4; i++) Assert.Less(_store.Level(i, 0), 40f, "agent " + i);
    }

    [Test]
    public void SolverBuildsAnEdgeForAColonyWithFreePoints()
    {
        var registry = new ColonyRegistry();
        try
        {
            var colony = registry.Add(float2.zero, null, 8);
            var members = new EntityModel[3];
            for (int i = 0; i < 3; i++)
            {
                members[i] = new EntityModel { ManagerIndex = i };
                colony.AddMember(members[i]);
                _store.Reset(i, Genes(), colony.SkillTopology, _atlas);
                _store.SetColony(i, colony.Index);
                for (int v = 0; v < _atlas.Count; v++) _store.SetLevel(i, v, 30f, 0f);
                _store.ClearDirty(i);
            }

            var topology = colony.SkillTopology;
            int revision = topology.Revision;
            int rootEdges = CountEdges(topology);

            var solver = new SkillGraphSolver();
            int changes = solver.Step(colony, _store, _atlas, _rules);

            Assert.Greater(changes, 0);
            Assert.Greater(topology.Revision, revision);
            Assert.Greater(CountEdges(topology), rootEdges);
            Assert.Greater(topology.PhiPeak, 0f);
            Assert.LessOrEqual(topology.SpentEdgePoints, topology.Earned(in SimulationRules.Frame));
            for (int i = 0; i < 3; i++) Assert.IsTrue(_store.IsDirty(i));
        }
        finally
        {
            registry.Dispose();
        }
    }

    [Test]
    public void SolverDoesNothingWithoutMembersOrPoints()
    {
        var registry = new ColonyRegistry();
        try
        {
            var colony = registry.Add(float2.zero, null, 8);
            var solver = new SkillGraphSolver();
            int revision = colony.SkillTopology.Revision;
            Assert.AreEqual(0, solver.Step(colony, _store, _atlas, _rules));

            var member = new EntityModel { ManagerIndex = 0 };
            colony.AddMember(member);
            _store.Reset(0, Genes(), colony.SkillTopology, _atlas);
            Assert.AreEqual(0, solver.Step(colony, _store, _atlas, _rules));
            Assert.AreEqual(revision, colony.SkillTopology.Revision);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [Test]
    public void XpAwardNeverAllocates()
    {
        _store.Reset(0, Genes(), _topology, _atlas);
        _store.SetColony(0, 0);
        int action = (int)EntityAction.MoveTo;
        Assert.IsTrue(_atlas.TryGetXpRange(action, out _, out _));
        float now = 0f;
        AllocationAssert.Zero(() =>
        {
            now += 1f;
            SkillGraphXp.Award(_store, 0, action, now, _topology, _atlas, in SimulationRules.Frame);
        }, 200, "SkillGraphXp.Award");
    }

    [Test]
    public void XpAwardIgnoresActionsWithoutSources()
    {
        _store.Reset(0, Genes(), _topology, _atlas);
        Assert.AreEqual(0, SkillGraphXp.Award(_store, 0, -1, 1f, _topology, _atlas, in SimulationRules.Frame));
        Assert.AreEqual(0, SkillGraphXp.Award(_store, 0, ActionCatalog.Count, 1f, _topology, _atlas, in SimulationRules.Frame));
        Assert.AreEqual(0, SkillGraphXp.Award(_store, 0, (int)EntityAction.Idle, 1f, _topology, _atlas, in SimulationRules.Frame));
        for (int v = 0; v < _atlas.Count; v++) Assert.AreEqual(0f, _store.Peak(0, v));
    }

    [Test]
    public void XpAwardRaisesAnAvailableSkillOfTheAction()
    {
        _store.Reset(0, Genes(), _topology, _atlas);
        int root = _store.Root(0);
        int action = -1;
        for (int a = 0; a < ActionCatalog.Count && action < 0; a++)
            if (_atlas.TryGetXpRange(a, out int start, out int length))
                for (int i = 0; i < length; i++)
                    if (_atlas.XpSourceAt(start + i).Skill == root) action = a;
        if (action < 0) Assert.Ignore("the root skill of this atlas has no XP source");

        for (int k = 0; k < 30; k++) SkillGraphXp.Award(_store, 0, action, k, _topology, _atlas, in SimulationRules.Frame);
        Assert.Greater(_store.Peak(0, root), 0f);
    }

    private static int CountEdges(SkillGraphTopology t)
    {
        int edges = 0;
        for (int v = 0; v < t.Count; v++) edges += math.countbits(t.ActiveRow(v));
        return edges / 2;
    }

    private sealed class AtlasHandle : System.IDisposable
    {
        public readonly SkillAtlas Atlas;
        public AtlasHandle(SkillAtlas atlas) => Atlas = atlas;
        public void Dispose() => Object.DestroyImmediate(Atlas);
    }

    private sealed class TopologyHandle : System.IDisposable
    {
        public readonly SkillGraphTopology Topology;
        public TopologyHandle(SkillGraphTopology topology) => Topology = topology;
        public void Dispose() => Topology.Dispose();
    }
}
