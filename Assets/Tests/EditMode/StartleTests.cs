using NUnit.Framework;
using Unity.Collections;

public class StartleTests
{
    private NativeArray<InstinctInput>  _inputs;
    private NativeArray<InstinctGenes>  _genes;
    private NativeArray<InstinctState>  _states;
    private NativeArray<InstinctOutput> _outputs;
    private InstinctRules _rules;

    [SetUp]
    public void SetUp()
    {
        _inputs  = new NativeArray<InstinctInput>(1, Allocator.Persistent);
        _genes   = new NativeArray<InstinctGenes>(1, Allocator.Persistent);
        _states  = new NativeArray<InstinctState>(1, Allocator.Persistent);
        _outputs = new NativeArray<InstinctOutput>(1, Allocator.Persistent);
        _genes[0] = new InstinctGenes { CarryFull = 1f, SatedFill = 0.5f };
        _rules = new InstinctRules { EatRange = 1f, CarryFull = 1f, JuvenileTime = 1f, StartleStress = 0.3f, StartleCooldown = 2f };
    }

    [TearDown]
    public void TearDown()
    {
        _inputs.Dispose();
        _genes.Dispose();
        _states.Dispose();
        _outputs.Dispose();
    }

    private InstinctOutput Step(float now, bool threat, bool plan)
    {
        _inputs[0] = new InstinctInput
        {
            HpFraction = 1f, EnergyFraction = 1f, Stomach = 1f, FoodDistance = -1f, ParentDistance = -1f, NestDistance = -1f,
            Age = 100f, Now = now, Dt = 1f,
            HasThreat = (byte)(threat ? 1 : 0),
            PlanActive = (byte)(plan ? 1 : 0),
            PlanKind = (byte)PlanKind.Patrol,
            RunningAction = InstinctBits.NoReflex,
        };
        new InstinctJob { Offset = 0, R = _rules, Inputs = _inputs, Genes = _genes, States = _states, Outputs = _outputs }.Execute(0);
        return _outputs[0];
    }

    [Test]
    public void FrontOfThreatInterruptsPlanAndAddsStress()
    {
        Step(10f, false, true);
        var o = Step(11f, true, true);
        Assert.AreEqual(1, o.Interrupt);
        Assert.AreEqual(1, o.NeedsDecision);
        Assert.AreEqual(_rules.StartleStress, o.StartleStress);
        Assert.AreNotEqual(0u, o.Mask & InstinctBits.Startle);
    }

    [Test]
    public void HeldThreatDoesNotStartleAgain()
    {
        Step(10f, false, true);
        Step(11f, true, true);
        var o = Step(12f, true, true);
        Assert.AreEqual(0, o.Interrupt);
        Assert.AreEqual(0f, o.StartleStress);
    }

    [Test]
    public void SecondFrontInsideCooldownIsIgnored()
    {
        Step(10f, false, true);
        Step(11f, true, true);
        Step(11.5f, false, true);
        var o = Step(12f, true, true);
        Assert.AreEqual(0f, o.StartleStress);
        Step(12.5f, false, true);
        o = Step(14f, true, true);
        Assert.AreEqual(_rules.StartleStress, o.StartleStress);
    }
}

public class FeedingLatchTests
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
        _genes[0]  = new InstinctGenes { CarryFull = 1f, SatedFill = 0.5f };
        _states[0] = new InstinctState { Latches = InstinctBits.Feeding };
    }

    [TearDown]
    public void TearDown()
    {
        _inputs.Dispose();
        _genes.Dispose();
        _states.Dispose();
        _outputs.Dispose();
    }

    private InstinctOutput Step(bool eatFailed)
    {
        _inputs[0] = new InstinctInput
        {
            HpFraction = 1f, EnergyFraction = 1f, Stomach = 0f, FoodDistance = 0.5f, ParentDistance = -1f, NestDistance = -1f,
            Age = 100f, Now = 10f, Dt = 1f, RunningAction = InstinctBits.NoReflex,
            EatFailed = (byte)(eatFailed ? 1 : 0),
        };
        var rules = new InstinctRules { EatRange = 1f, CarryFull = 1f, JuvenileTime = 1f };
        new InstinctJob { Offset = 0, R = rules, Inputs = _inputs, Genes = _genes, States = _states, Outputs = _outputs }.Execute(0);
        return _outputs[0];
    }

    [Test]
    public void LatchKeepsEatReflexWhileLastEatDidNotFail()
    {
        var o = Step(false);
        Assert.AreEqual((byte)EntityAction.Eat, o.Reflex);
        Assert.AreNotEqual(0u, o.Latches & InstinctBits.Feeding);
    }

    [Test]
    public void FailedEatClearsLatchAndEatReflexNeedsHungerAgain()
    {
        var o = Step(true);
        Assert.AreEqual(InstinctBits.NoReflex, o.Reflex);
        Assert.AreEqual(0u, o.Latches & InstinctBits.Feeding);
    }
}
