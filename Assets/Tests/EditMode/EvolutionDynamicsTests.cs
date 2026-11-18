using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public class EvolutionDynamicsTests
{
    private const int K = (int)VarianceAxis.Count;

    private sealed class NoPurse : ICodexPurse
    {
        public float Balance(int authorId) => 0f;
        public bool TryDebit(int authorId, float amount) => false;
    }

    private SimulationRules _baseRules;
    private SimulationRules _applied;

    [SetUp]
    public void SetUp()
    {
        SimulationClock.Reset();
        _baseRules = ScriptableObject.CreateInstance<SimulationRules>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_applied != null) Object.DestroyImmediate(_applied);
        Object.DestroyImmediate(_baseRules);
        SimulationRules.Bind(null);
        SimulationClock.Reset();
    }

    private void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    [Test]
    public void ColonyGenomeStartsInRangeAndIsInheritedWithMutation()
    {
        for (int i = 0; i < 50; i++)
        {
            var g = ColonyGenome.Random();
            var lr = GeneticParameters.ParameterRanges[GeneticParameters.IdxBaseLearningRate];
            var gn = GeneticParameters.ParameterRanges[GeneticParameters.IdxMaxGradientNorm];
            var er = GeneticParameters.ParameterRanges[GeneticParameters.IdxEntropyRegularization];
            Assert.That(g.BaseLearningRate, Is.InRange(lr.min, lr.max));
            Assert.That(g.MaxGradientNorm, Is.InRange(gn.min, gn.max));
            Assert.That(g.EntropyRegularization, Is.InRange(er.min, er.max));
        }

        var parent = ColonyGenome.Random();
        var same = parent.Inherited(0f);
        Assert.AreEqual(parent.BaseLearningRate, same.BaseLearningRate);
        Assert.AreEqual(parent.MaxGradientNorm, same.MaxGradientNorm);
        Assert.AreEqual(parent.EntropyRegularization, same.EntropyRegularization);

        bool moved = false;
        for (int i = 0; i < 20; i++)
        {
            var child = parent.Inherited(1f);
            var lr = GeneticParameters.ParameterRanges[GeneticParameters.IdxBaseLearningRate];
            Assert.That(child.BaseLearningRate, Is.InRange(lr.min, lr.max));
            moved |= child.BaseLearningRate != parent.BaseLearningRate || child.MaxGradientNorm != parent.MaxGradientNorm
                  || child.EntropyRegularization != parent.EntropyRegularization;
        }
        Assert.IsTrue(moved);
    }

    [Test]
    public void ColonyOwnsTheGenomeAndPassesItOnWhenReseeded()
    {
        SimulationRules.Bind(_baseRules);
        SetField(_baseRules, "colonyGenomeMutationChance", 0f);
        SimulationRules.Bind(_baseRules);

        var registry = new ColonyRegistry();
        try
        {
            var donor = registry.Add(new float2(0f, 0f), null, 4);
            var heir  = registry.Add(new float2(50f, 0f), null, 4);
            Assert.AreNotEqual(default(ColonyGenome).BaseLearningRate, donor.Genome.BaseLearningRate);

            heir.InheritGenome(donor.Genome);
            Assert.AreEqual(donor.Genome.BaseLearningRate, heir.Genome.BaseLearningRate);
            Assert.AreEqual(donor.Genome.MaxGradientNorm, heir.Genome.MaxGradientNorm);
            Assert.AreEqual(donor.Genome.EntropyRegularization, heir.Genome.EntropyRegularization);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [Test]
    public void PerturbationScalesItsAxisOnlyInsideItsWindow()
    {
        var schedule = new[]
        {
            new PerturbationEntry { startTime = 10f, duration = 20f, axis = VarianceAxis.FoodGrowth, multiplier = 2f },
        };
        SetField(_baseRules, "perturbationSchedule", schedule);
        _applied = _baseRules.CreateLawApplied();
        var codex = new WorldCodexService(_applied, new EnergyLedger(), new NoPurse());
        float baseline = _baseRules.FoodGrowthRate;

        SimulationClock.SetTime(0.0);
        codex.Step(0f);
        Assert.AreEqual(0, codex.PerturbationActive);
        Assert.AreEqual(baseline, _applied.FoodGrowthRate, 1e-6f);

        SimulationClock.SetTime(15.0);
        codex.Step(1f);
        Assert.AreEqual(1, codex.PerturbationActive);
        Assert.AreEqual(baseline * 2f, _applied.FoodGrowthRate, 1e-5f * baseline);

        SimulationClock.SetTime(31.0);
        codex.Step(1f);
        Assert.AreEqual(0, codex.PerturbationActive);
        Assert.AreEqual(baseline, _applied.FoodGrowthRate, 1e-5f * baseline);
    }

    [Test]
    public void EmptyScheduleLeavesTheLawAppliedRulesUntouched()
    {
        _applied = _baseRules.CreateLawApplied();
        var codex = new WorldCodexService(_applied, new EnergyLedger(), new NoPurse());
        SimulationClock.SetTime(100.0);
        codex.Step(1f);
        Assert.AreEqual(0, codex.PerturbationActive);
        Assert.AreEqual(_baseRules.FoodGrowthRate, _applied.FoodGrowthRate);
        Assert.AreEqual(0, _baseRules.PerturbationSchedule.Length);
    }

    [Test]
    public void InvalidScheduleEntriesAreIgnored()
    {
        var schedule = new[]
        {
            new PerturbationEntry { startTime = 0f, duration = 100f, axis = VarianceAxis.FoodGrowth, multiplier = 0f },
            new PerturbationEntry { startTime = 0f, duration = 100f, axis = VarianceAxis.FoodGrowth, multiplier = float.NaN },
            new PerturbationEntry { startTime = 0f, duration = 100f, axis = (VarianceAxis)99, multiplier = 2f },
        };
        SetField(_baseRules, "perturbationSchedule", schedule);
        _applied = _baseRules.CreateLawApplied();
        var codex = new WorldCodexService(_applied, new EnergyLedger(), new NoPurse());

        SimulationClock.SetTime(5.0);
        codex.Step(1f);
        Assert.AreEqual(0, codex.PerturbationActive);
        Assert.AreEqual(_baseRules.FoodGrowthRate, _applied.FoodGrowthRate);
    }
}
