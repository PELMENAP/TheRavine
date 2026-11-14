using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class PredatorPressureTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = TestRules.Bind();

    [TearDown]
    public void TearDown() => TestRules.Release(_rules);

    [Test]
    public void PressureRelaxesQuorumForEmergencyRelocation()
    {
        var registry = new ColonyRegistry();
        var colony = registry.Add(new float2(0f, 0f), null, 8);
        var site = new float2(100f, 0f);
        colony.VoteNestSite(site, 1, 1f);
        colony.VoteNestSite(site, 2, 1f);

        Assert.IsFalse(colony.TryGetQuorumSite(out _));
        colony.RaisePressure(1f);
        Assert.IsTrue(colony.TryGetQuorumSite(out _));
    }

    [Test]
    public void PressureDecaysAndStaysInUnitRange()
    {
        var registry = new ColonyRegistry();
        var colony = registry.Add(new float2(0f, 0f), null, 8);
        colony.RaisePressure(5f);
        Assert.AreEqual(1f, colony.Pressure);
        colony.TickEma(_rules.PressureTau);
        Assert.AreEqual(Mathf.Exp(-1f), colony.Pressure, 1e-4f);
        colony.RaisePressure(-1f);
        Assert.AreEqual(Mathf.Exp(-1f), colony.Pressure, 1e-4f);
    }

    [Test]
    public void PredatorFearIsScaledByRuleAndColonyCastesAreNeutral()
    {
        Assert.AreEqual(_rules.PredatorFearMul, CasteModifiers.Predator().Fear);
        Assert.AreEqual(_rules.PredatorHuntBias, CasteModifiers.Predator().HuntBias);
        Assert.AreEqual(1f, CasteModifiers.For(Caste.Worker).Fear);
        Assert.AreEqual(1f, CasteModifiers.For(Caste.Soldier).Fear);
        Assert.AreEqual(0f, CasteModifiers.For(Caste.Soldier).HuntBias);
    }
}

public class PackLeashTests
{
    private NativeArray<InstinctInput>  _inputs;
    private NativeArray<InstinctGenes>  _genes;
    private NativeArray<InstinctState>  _states;
    private NativeArray<InstinctOutput> _outputs;

    [SetUp]
    public void SetUp()
    {
        _inputs  = new NativeArray<InstinctInput>(1, Allocator.Persistent);
        _genes   = new NativeArray<InstinctGenes>(1, Allocator.Persistent);
        _states  = new NativeArray<InstinctState>(1, Allocator.Persistent);
        _outputs = new NativeArray<InstinctOutput>(1, Allocator.Persistent);
        _genes[0] = new InstinctGenes { CarryFull = 1f, SatedFill = 0.5f, Tether = 1f };
    }

    [TearDown]
    public void TearDown()
    {
        _inputs.Dispose();
        _genes.Dispose();
        _states.Dispose();
        _outputs.Dispose();
    }

    private InstinctOutput Step(float packDistance, bool threat)
    {
        _inputs[0] = new InstinctInput
        {
            HpFraction = 1f, EnergyFraction = 1f, Stomach = 1f, FoodDistance = -1f, ParentDistance = -1f, NestDistance = -1f,
            Age = 100f, Now = 10f, Dt = 1f, RunningAction = InstinctBits.NoReflex, PackDistance = packDistance,
            HasThreat = (byte)(threat ? 1 : 0),
        };
        var rules = new InstinctRules { EatRange = 1f, CarryFull = 1f, JuvenileTime = 1f, PackRadius = 25f, PackRate = 1e6f };
        new InstinctJob { Offset = 0, R = rules, Inputs = _inputs, Genes = _genes, States = _states, Outputs = _outputs }.Execute(0);
        return _outputs[0];
    }

    [Test]
    public void StrayedPackmatePullsTowardKin()
    {
        var o = Step(100f, false);
        Assert.AreEqual((byte)EntityAction.MoveTo, o.Reflex);
        Assert.AreEqual((byte)DirectionKind.Kin, o.Direction);
        Assert.AreNotEqual(0u, o.Mask & InstinctBits.Pack);
    }

    [Test]
    public void PackmateInsideRadiusOrWithoutPackIsNotPulled()
    {
        Assert.AreEqual(InstinctBits.NoReflex, Step(5f, false).Reflex);
        Assert.AreEqual(InstinctBits.NoReflex, Step(-1f, false).Reflex);
    }

    [Test]
    public void SeeingPreyOverridesTheLeash()
    {
        Assert.AreEqual(InstinctBits.NoReflex, Step(100f, true).Reflex);
    }
}
