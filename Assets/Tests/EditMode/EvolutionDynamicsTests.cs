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
    public void ColonyGenomeStartsAtTheCentreAndSpreadWidensIt()
    {
        var lr = GeneticParameters.ParameterRanges[GeneticParameters.IdxBaseLearningRate];
        var gn = GeneticParameters.ParameterRanges[GeneticParameters.IdxMaxGradientNorm];
        var er = GeneticParameters.ParameterRanges[GeneticParameters.IdxEntropyRegularization];

        var centre = ColonyGenome.Initial(0f);
        Assert.AreEqual(0.5f * (lr.min + lr.max), centre.BaseLearningRate, 1e-6f);
        Assert.AreEqual(0.5f * (gn.min + gn.max), centre.MaxGradientNorm, 1e-6f);
        Assert.AreEqual(0.5f * (er.min + er.max), centre.EntropyRegularization, 1e-6f);

        bool differs = false;
        for (int i = 0; i < 50; i++)
        {
            var g = ColonyGenome.Initial(1f);
            Assert.That(g.BaseLearningRate, Is.InRange(lr.min, lr.max));
            Assert.That(g.MaxGradientNorm, Is.InRange(gn.min, gn.max));
            Assert.That(g.EntropyRegularization, Is.InRange(er.min, er.max));
            differs |= g.BaseLearningRate != centre.BaseLearningRate;
        }
        Assert.IsTrue(differs);
    }

    [Test]
    public void ColonyGenomeIsInheritedWithMutation()
    {
        var parent = ColonyGenome.Initial(1f);
        var same = parent.Inherited(0f);
        Assert.AreEqual(parent.BaseLearningRate, same.BaseLearningRate);
        Assert.AreEqual(parent.MaxGradientNorm, same.MaxGradientNorm);
        Assert.AreEqual(parent.EntropyRegularization, same.EntropyRegularization);

        var lr = GeneticParameters.ParameterRanges[GeneticParameters.IdxBaseLearningRate];
        bool moved = false;
        for (int i = 0; i < 20; i++)
        {
            var child = parent.Inherited(1f);
            Assert.That(child.BaseLearningRate, Is.InRange(lr.min, lr.max));
            moved |= child.BaseLearningRate != parent.BaseLearningRate || child.MaxGradientNorm != parent.MaxGradientNorm
                  || child.EntropyRegularization != parent.EntropyRegularization;
        }
        Assert.IsTrue(moved);
    }

    [Test]
    public void ColoniesStartWithTheSameGenomeUnlessSpreadIsSet()
    {
        SimulationRules.Bind(_baseRules);
        var registry = new ColonyRegistry();
        try
        {
            var a = registry.Add(new float2(0f, 0f), null, 4);
            var b = registry.Add(new float2(50f, 0f), null, 4);
            Assert.AreEqual(a.Genome.BaseLearningRate, b.Genome.BaseLearningRate);
            Assert.AreEqual(a.Genome.MaxGradientNorm, b.Genome.MaxGradientNorm);
            Assert.AreEqual(a.Genome.EntropyRegularization, b.Genome.EntropyRegularization);

            SetField(_baseRules, "colonyGenomeInitialSpread", 1f);
            bool differs = false;
            for (int i = 0; i < 10 && !differs; i++)
                differs |= registry.Add(new float2(100f + i, 0f), null, 4).Genome.BaseLearningRate != a.Genome.BaseLearningRate;
            Assert.IsTrue(differs);
        }
        finally
        {
            registry.Dispose();
        }
    }

    [Test]
    public void ReseedDonorIsChosenInsideTheSameArm()
    {
        SimulationRules.Bind(_baseRules);
        var registry = new ColonyRegistry();
        try
        {
            SetField(_baseRules, "armNullPolicyFraction", 1f);
            var nullA = registry.Add(new float2(0f, 0f), null, 4);
            var nullB = registry.Add(new float2(10f, 0f), null, 4);
            SetField(_baseRules, "armNullPolicyFraction", 0f);
            var controlA = registry.Add(new float2(20f, 0f), null, 4);
            var controlB = registry.Add(new float2(30f, 0f), null, 4);
            Assert.AreEqual(ColonyArm.NullPolicy, nullA.Arm);
            Assert.AreEqual(ColonyArm.Control, controlA.Arm);

            controlB.AddMember(new EntityModel { ManagerIndex = 0 });
            Assert.IsNull(ColonyPopulation.PickDonor(registry, nullA));
            Assert.AreSame(controlB, ColonyPopulation.PickDonor(registry, controlA));

            nullB.AddMember(new EntityModel { ManagerIndex = 1 });
            Assert.AreSame(nullB, ColonyPopulation.PickDonor(registry, nullA));
            Assert.AreSame(controlB, ColonyPopulation.PickDonor(registry, controlA));
        }
        finally
        {
            registry.Dispose();
        }
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
