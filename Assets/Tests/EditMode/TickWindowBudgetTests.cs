using NUnit.Framework;

public class TickWindowBudgetTests
{
    [SetUp]
    public void SetUp() => SimulationClock.Reset();

    [TearDown]
    public void TearDown() => SimulationClock.Reset();

    [TestCase(1f / 64f, 64)]
    [TestCase(1f / 32f, 32)]
    [TestCase(0.25f, 4)]
    [TestCase(0.5f, 2)]
    [TestCase(1f, 1)]
    [TestCase(2f, 1)]
    [TestCase(0f, TickWindowBudget.MaxFrames)]
    [TestCase(1f / 1024f, TickWindowBudget.MaxFrames)]
    public void FramesFollowSimulationDt(float dt, int expected)
        => Assert.AreEqual(expected, TickWindowBudget.Frames(dt));

    [TestCase(200, 64, 4)]
    [TestCase(17, 4, 5)]
    [TestCase(1, 240, 1)]
    [TestCase(0, 32, 1)]
    public void BatchSizeCoversWholeSnapshot(int count, int frames, int expected)
        => Assert.AreEqual(expected, TickWindowBudget.BatchSize(count, frames));

    [TestCase(1f / 64f, 200)]
    [TestCase(1f / 32f, 200)]
    [TestCase(0.25f, 17)]
    public void WindowBudgetUsesSimulationClockOnly(float dt, int agents)
    {
        var budget = new TickWindowBudget();
        int frames = TickWindowBudget.Frames(dt);
        int expected = TickWindowBudget.BatchSize(agents, frames);

        Assert.AreEqual(expected, budget.BeginWindow(SimulationClock.TimeD, dt, agents));

        for (int i = 0; i < frames; i++)
        {
            budget.CountFrame();
            SimulationClock.Advance(dt);
        }

        Assert.AreEqual(dt, SimulationClock.DeltaTime);
        Assert.AreEqual(frames, budget.FramesCounted);
        Assert.AreEqual(expected, budget.BeginWindow(SimulationClock.TimeD, 123f, agents));
        Assert.AreEqual(0, budget.FramesCounted);
    }

    [Test]
    public void WindowBudgetTracksSimulationTimeScale()
    {
        var budget = new TickWindowBudget();
        budget.BeginWindow(SimulationClock.TimeD, 1f / 64f, 128);

        for (int i = 0; i < 64; i++)
        {
            budget.CountFrame();
            SimulationClock.Advance(2f / 64f);
        }

        Assert.AreEqual(TickWindowBudget.BatchSize(128, 32), budget.BeginWindow(SimulationClock.TimeD, 1f / 64f, 128));
    }

    [Test]
    public void PausedClockClampsToMaxFrames()
    {
        var budget = new TickWindowBudget();
        budget.BeginWindow(SimulationClock.TimeD, 1f / 64f, 480);

        for (int i = 0; i < 10; i++)
        {
            budget.CountFrame();
            SimulationClock.Advance(0f);
        }

        Assert.AreEqual(0f, SimulationClock.DeltaTime);
        Assert.AreEqual(TickWindowBudget.BatchSize(480, TickWindowBudget.MaxFrames),
            budget.BeginWindow(SimulationClock.TimeD, 1f / 64f, 480));
    }
}
