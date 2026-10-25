using NUnit.Framework;

public class FitnessComponentTests
{
    [SetUp]
    public void SetUp() => SimulationRules.Bind(null);

    [Test]
    public void EventsRaiseFitness()
    {
        var f = new FitnessComponent();
        float baseline = f.Evaluate(30f);
        f.Register(EntityModel.FitnessEvent.FoodEaten, 0f);
        float fed = f.Evaluate(30f);
        Assert.Greater(fed, baseline);
        f.Register(EntityModel.FitnessEvent.DamageDealt, 10f);
        Assert.AreEqual(1, f.FoodEaten);
        Assert.AreEqual(10f, f.DamageDealt);
        Assert.GreaterOrEqual(f.Evaluate(30f), fed);
    }

    [Test]
    public void CacheTracksTimeAndEvents()
    {
        var f = new FitnessComponent();
        float a = f.Evaluate(10f);
        Assert.AreEqual(a, f.Evaluate(10f));
        Assert.AreNotEqual(a, f.Evaluate(100f));
        f.Register(EntityModel.FitnessEvent.Reproduced, 0f);
        Assert.AreEqual(1, f.ReproduceCount);
        Assert.AreNotEqual(a, f.Evaluate(10f));
    }

    [Test]
    public void CaptureFreezesFinalFitness()
    {
        var f = new FitnessComponent();
        f.Register(EntityModel.FitnessEvent.FoodEaten, 0f);
        f.Capture(42f);
        Assert.AreEqual(f.Evaluate(42f), f.FinalFitness);
    }

    [Test]
    public void EvaluateDoesNotAllocate()
    {
        var f = new FitnessComponent();
        float t = 1f;
        AllocationAssert.Zero(() =>
        {
            t += 0.5f;
            f.Register(EntityModel.FitnessEvent.FoodEaten, 0f);
            f.Evaluate(t);
        }, 200, "FitnessComponent.Evaluate");
    }
}
