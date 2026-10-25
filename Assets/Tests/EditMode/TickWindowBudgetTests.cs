using NUnit.Framework;

public class TickWindowBudgetTests
{
    private static int RunWindow(ref TickWindowBudget budget, float dt, float interval, int count, int frames)
    {
        int cursor = 0;
        for (int f = 0; f < frames; f++)
        {
            cursor += budget.Take(dt, interval, count, count - cursor);
            if (cursor >= count) cursor = 0;
        }
        return cursor;
    }

    [TestCase(1f / 64f, 64, 200)]
    [TestCase(1f / 32f, 32, 200)]
    [TestCase(1f / 64f, 64, 17)]
    [TestCase(0.25f, 4, 8)]
    public void EveryAgentTicksOncePerInterval(float dt, int framesPerInterval, int count)
    {
        var budget = new TickWindowBudget();
        int total = 0;
        for (int f = 0; f < framesPerInterval; f++)
            total += budget.Take(dt, 1f, count, count - total);

        Assert.AreEqual(count, total);
    }

    [TestCase(1f / 64f)]
    [TestCase(1f / 32f)]
    [TestCase(1f / 128f)]
    public void TickRateDependsOnlyOnSimulationTime(float dt)
    {
        var budget = new TickWindowBudget();
        const int count = 64;
        int frames = (int)(4f / dt);
        int total = 0;
        for (int f = 0; f < frames; f++)
            total += budget.Take(dt, 1f, count, count);

        Assert.AreEqual(4 * count, total);
    }

    [Test]
    public void IntervalScalesRate()
    {
        var budget = new TickWindowBudget();
        int total = 0;
        for (int f = 0; f < 64; f++) total += budget.Take(1f / 64f, 0.5f, 32, 64);
        Assert.AreEqual(64, total);
    }

    [Test]
    public void SingleAgentWaitsForFullInterval()
    {
        var budget = new TickWindowBudget();
        for (int f = 0; f < 63; f++) Assert.AreEqual(0, budget.Take(1f / 64f, 1f, 1, 1));
        Assert.AreEqual(1, budget.Take(1f / 64f, 1f, 1, 1));
    }

    [Test]
    public void PausedClockTicksNothing()
    {
        var budget = new TickWindowBudget();
        for (int f = 0; f < 100; f++) Assert.AreEqual(0, budget.Take(0f, 1f, 50, 50));
        Assert.AreEqual(0d, budget.Debt);
    }

    [Test]
    public void HitchCatchesUpAtMostOneWindow()
    {
        var budget = new TickWindowBudget();
        Assert.AreEqual(50, budget.Take(10f, 1f, 50, 50));
        Assert.AreEqual(0d, budget.Debt);
        Assert.AreEqual(0, budget.Take(1f / 1024f, 1f, 50, 50));
    }

    [Test]
    public void RemainingCapsBatchAndCarriesDebt()
    {
        var budget = new TickWindowBudget();
        Assert.AreEqual(10, budget.Take(0.5f, 1f, 50, 10));
        Assert.AreEqual(15d, budget.Debt);
        Assert.AreEqual(15, budget.Take(1f / 1024f, 1f, 50, 50));
    }

    [Test]
    public void EmptyPopulationResetsDebt()
    {
        var budget = new TickWindowBudget();
        budget.Take(0.3f, 1f, 10, 10);
        Assert.AreEqual(0, budget.Take(1f, 1f, 0, 0));
        Assert.AreEqual(0d, budget.Debt);
    }

    [Test]
    public void WindowClosesAfterExactlyOneInterval()
    {
        var budget = new TickWindowBudget();
        Assert.AreEqual(0, RunWindow(ref budget, 1f / 64f, 1f, 96, 64));
    }

    [Test]
    public void SimulationClockReportsLastDelta()
    {
        SimulationClock.Reset();
        SimulationClock.Advance(1f / 64f);
        Assert.AreEqual(1f / 64f, SimulationClock.DeltaTime);
        SimulationClock.Advance(0f);
        Assert.AreEqual(0f, SimulationClock.DeltaTime);
        SimulationClock.Reset();
    }
}
