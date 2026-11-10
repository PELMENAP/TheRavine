using NUnit.Framework;
using TheRavine.EntityControl;

public class RateBudgetTests
{
    private const float Rate = 20f;
    private const float Burst = 8f;

    [Test]
    public void SteadyTrafficBelowTheRateIsAlwaysAccepted()
    {
        var budget = new RateBudget();

        for (int i = 0; i < 200; i++)
            Assert.IsTrue(budget.TryConsume(i * 0.1, Rate, Burst), $"request {i}");
    }

    [Test]
    public void HundredTimesTheRateIsCappedNearTheBudget()
    {
        var budget = new RateBudget();
        double normalPerSecond = 10.0;
        double spamPerSecond = normalPerSecond * 100.0;
        double duration = 2.0;
        int accepted = 0;
        int total = (int)(spamPerSecond * duration);

        for (int i = 0; i < total; i++)
            if (budget.TryConsume(i / spamPerSecond, Rate, Burst)) accepted++;

        Assert.LessOrEqual(accepted, Burst + Rate * duration + 2);
        Assert.GreaterOrEqual(accepted, Rate * duration);
    }

    [Test]
    public void InstantBurstIsBounded()
    {
        var budget = new RateBudget();
        int accepted = 0;

        for (int i = 0; i < 1000; i++)
            if (budget.TryConsume(5.0, Rate, Burst)) accepted++;

        Assert.GreaterOrEqual(accepted, (int)Burst);
        Assert.LessOrEqual(accepted, (int)Burst + 2);
    }

    [Test]
    public void BudgetRecoversAfterAPause()
    {
        var budget = new RateBudget();
        for (int i = 0; i < 1000; i++) budget.TryConsume(1.0, Rate, Burst);
        Assert.IsFalse(budget.TryConsume(1.0, Rate, Burst));

        Assert.IsTrue(budget.TryConsume(3.0, Rate, Burst));
    }

    [TestCase(0f, 8f)]
    [TestCase(-1f, 8f)]
    [TestCase(float.NaN, 8f)]
    [TestCase(20f, 0f)]
    [TestCase(20f, -3f)]
    [TestCase(20f, float.NaN)]
    public void InvalidParametersRefuseEverything(float rate, float burst)
    {
        var budget = new RateBudget();

        Assert.IsFalse(budget.TryConsume(0.0, rate, burst));
        Assert.IsFalse(budget.TryConsume(100.0, rate, burst));
    }

    [Test]
    public void BudgetsAreIndependent()
    {
        var movement = new RateBudget();
        var craft = new RateBudget();
        for (int i = 0; i < 1000; i++) movement.TryConsume(0.0, Rate, Burst);

        Assert.IsTrue(craft.TryConsume(0.0, 5f, 5f));
    }
}
