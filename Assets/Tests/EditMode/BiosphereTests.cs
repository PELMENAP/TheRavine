using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.EntityControl.Biosphere;
using TheRavine.Extensions;

public class BiosphereTests
{
    private SimulationRules _rules;
    private BiosphereRules _bio;

    [SetUp]
    public void SetUp()
    {
        _rules = TestRules.Bind();
        _bio = ScriptableObject.CreateInstance<BiosphereRules>();
        _bio.simulationEnabled = true;
        _bio.minOrganisms = 0;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_bio);
        TestRules.Release(_rules);
    }

    [Test]
    public void DefaultReactionsAreBalancedAndUnbalancedAreDropped()
    {
        var table = new ChemistryTable(_bio.substances, _bio.reactions);
        Assert.AreEqual(0, table.InvalidReactions);
        Assert.AreEqual(_bio.reactions.Length, table.ReactionCount);

        var broken = new[] { new ReactionDef(0, -1, 1, -1), new ReactionDef(0, 1, 4, -1) };
        var partial = new ChemistryTable(_bio.substances, broken);
        Assert.AreEqual(1, partial.InvalidReactions);
        Assert.AreEqual(1, partial.ReactionCount);
    }

    [Test]
    public void SolverConservesElementsAndEnergyWithoutNegatives()
    {
        var table = new ChemistryTable(_bio.substances, _bio.reactions);
        var rng = new System.Random(4);
        for (int trial = 0; trial < 50; trial++)
        {
            var pool = new float[table.SubstanceCount];
            for (int s = 0; s < pool.Length; s++) pool[s] = (float)rng.NextDouble() * 5f;
            var activity = new float[table.ReactionCount];
            for (int k = 0; k < activity.Length; k++) activity[k] = (float)rng.NextDouble();
            float energy = (float)rng.NextDouble() * 3f;

            float4 elements = table.ElementsOf(pool);
            float total = energy + table.BondEnergy(pool);
            float lost = 0f;
            for (int step = 0; step < 40; step++)
            {
                ReactionSolver.Step(table, pool, ref energy, activity, 0.7f, 0.5f, 0.8f, out float loss);
                lost += loss;
                foreach (float v in pool) Assert.GreaterOrEqual(v, 0f);
                Assert.GreaterOrEqual(energy, -1e-4f);
            }
            Assert.That(math.all(math.abs(table.ElementsOf(pool) - elements) < 1e-3f), $"elements trial {trial}");
            Assert.AreEqual(total, energy + table.BondEnergy(pool) + lost, 1e-3f, $"energy trial {trial}");
        }
    }

    [Test]
    public void SameResourceGivesDifferentOutcomesForDifferentProfiles()
    {
        var table = new ChemistryTable(_bio.substances, _bio.reactions);
        float Run(float[] activity)
        {
            var pool = new float[table.SubstanceCount];
            pool[0] = pool[1] = pool[2] = pool[3] = 4f;
            float energy = 10f;
            for (int i = 0; i < 50; i++) ReactionSolver.Step(table, pool, ref energy, activity, 0.5f, 1f, 0.8f, out _);
            return pool[_bio.structureSubstance];
        }
        var builder = new float[table.ReactionCount];
        builder[0] = builder[4] = builder[6] = 1f;
        var idle = new float[table.ReactionCount];
        Assert.Greater(Run(builder), 0.5f);
        Assert.AreEqual(0f, Run(idle));
    }

    [Test]
    public void FieldDiffusionConservesMass()
    {
        _bio.weatheringRate = 0f;
        var field = new ChemicalField(64);
        for (int x = 0; x < 5; x++)
            for (int z = 0; z < 5; z++)
                field.Add(Position2Int.Pack(x, z), new float4(x * 3f, z, 1f, 0f), _bio);
        float4 before = field.Total();
        for (int i = 0; i < 100; i++) field.Step(1f, _bio);
        Assert.That(math.all(math.abs(field.Total() - before) < 1e-2f));
    }

    private CellBiosphere Make(out EnergyLedger ledger)
    {
        ledger = new EnergyLedger();
        return new CellBiosphere(_bio, ledger);
    }

    private static void Program(CellBiosphere bio, int org, int target)
    {
        var w = bio.GeneWeights(org);
        w.Clear();
        var t = bio.GeneTargets(org);
        for (int g = 0; g < t.Length; g++) t[g] = 0;
        w[CellBiosphere.Inputs - 1] = 1f;
        t[0] = (byte)target;
        bio.ActivityOf(org).Clear();
    }

    [Test]
    public void DivisionGeneGrowsTheBody()
    {
        var bio = Make(out _);
        int org = bio.SeedAt(Position2Int.Pack(0, 0), 50f, 0f);
        Assert.GreaterOrEqual(org, 0);
        Program(bio, org, CellBiosphere.TargetDivide);
        bio.PoolOf(org)[_bio.structureSubstance] = 10f;

        float now = 0f;
        for (int i = 0; i < 12; i++) { bio.Step(now, default); now += _bio.stepInterval; }
        Assert.Greater(bio.CellsOf(org), 4);
        Assert.Greater(bio.Divisions, 3);
        for (int i = 0; i < bio.CellCount; i++)
        {
            Assert.IsTrue(bio.TryGetCell(i, out long key, out _, out int owner));
            Assert.IsTrue(bio.IsOccupied(key));
            Assert.AreEqual(org, owner);
        }
    }

    [Test]
    public void ApoptosisGeneKillsAndReturnsMaterial()
    {
        var bio = Make(out _);
        int org = bio.SeedAt(Position2Int.Pack(3, 3), 5f, 0f);
        Program(bio, org, CellBiosphere.TargetDie);
        float now = 0f;
        for (int i = 0; i < 4; i++) { bio.Step(now, default); now += _bio.stepInterval; }
        Assert.IsFalse(bio.IsAlive(org));
        Assert.AreEqual(0, bio.CellCount);
        Assert.AreEqual(1, bio.Deaths);
    }

    [Test]
    public void EnergyStockMatchesBookedFlows()
    {
        var bio = Make(out var ledger);
        float now = 0f;
        for (int k = 0; k < 6; k++)
        {
            int org = bio.SeedAt(Position2Int.Pack(k * 7, 0), 20f, 0f);
            bio.PoolOf(org)[0] = 3f;
            bio.PoolOf(org)[1] = 3f;
        }
        float before = bio.TotalEnergy();
        var flows = ledger.Flows;
        flows.BiosphereSeed = 0f;
        ledger.Flows = flows;
        float4 elementsBefore = bio.TotalElements();
        double sourceBefore = bio.Field.Source;

        for (int i = 0; i < 200; i++) { bio.Step(now, default); now += _bio.stepInterval; }

        var f = ledger.Flows;
        float expected = before + f.BiosphereLight + f.BiosphereSeed + f.BiosphereSoil - f.BiosphereUpkeep - f.SoilReturn;
        Assert.AreEqual(expected, bio.TotalEnergy(), 1e-2f * math.max(1f, before));
        float added = (float)(bio.Field.Source - sourceBefore);
        Assert.AreEqual(math.csum(elementsBefore) + added, math.csum(bio.TotalElements()), 0.05f);
    }

    [Test]
    public void SporeCreatesMutatedOffspring()
    {
        var bio = Make(out _);
        _bio.sporeMinCells = 1;
        int org = bio.SeedAt(Position2Int.Pack(0, 0), 100f, 0f);
        Program(bio, org, CellBiosphere.TargetSpore);
        float now = 0f;
        for (int i = 0; i < 6; i++) { bio.Step(now, default); now += _bio.stepInterval; }
        Assert.Greater(bio.Spores, 0);
        Assert.Greater(bio.OrganismCount, 1);
    }
}
