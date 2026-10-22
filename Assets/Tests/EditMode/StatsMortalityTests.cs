using NUnit.Framework;

public class StatsMortalityTests
{
    private static StatsComponent NewStats()
    {
        var stats = new StatsComponent();
        stats.FillComponent(100f, 50f);
        return stats;
    }

    [Test]
    public void FillStartsAtHalf()
    {
        var stats = NewStats();
        Assert.AreEqual(50f, stats.Hp);
        Assert.AreEqual(25f, stats.En);
    }

    [Test]
    public void CommitBelowZeroRaisesDeathOnce()
    {
        var stats     = NewStats();
        var mortality = new MortalityComponent(stats);
        int died = 0;
        mortality.Died += () => died++;

        stats.Open();
        stats.Hp = -5f;
        Assert.AreEqual(0, died);
        stats.Commit();
        Assert.AreEqual(1, died);
        Assert.IsTrue(mortality.IsDead);

        stats.Hp = -10f;
        stats.Hp = 0f;
        Assert.AreEqual(1, died);
    }

    [Test]
    public void DirectWriteRaisesDeath()
    {
        var stats     = NewStats();
        var mortality = new MortalityComponent(stats);
        int died = 0;
        mortality.Died += () => died++;

        stats.Hp = 10f;
        Assert.AreEqual(0, died);
        stats.Hp = 0f;
        Assert.AreEqual(1, died);
    }

    [Test]
    public void HarmCauseIsLockedByDeath()
    {
        var stats     = NewStats();
        var mortality = new MortalityComponent(stats);

        mortality.NoteHarm(DeathCause.Virus, 0f);
        stats.Hp = 0f;
        mortality.NoteHarm(DeathCause.Killed, 0f);
        Assert.AreEqual(DeathCause.Virus, mortality.Cause);
    }

    [Test]
    public void MortalityOnDepletedStatsDiesImmediately()
    {
        var stats = NewStats();
        stats.Hp = 0f;
        var mortality = new MortalityComponent(stats);
        Assert.IsTrue(mortality.IsDead);
    }

    [Test]
    public void DisposeStopsNotifications()
    {
        var stats     = NewStats();
        var mortality = new MortalityComponent(stats);
        int died = 0;
        mortality.Died += () => died++;
        mortality.Dispose();

        stats.Hp = 0f;
        Assert.AreEqual(0, died);
        Assert.IsFalse(mortality.IsDead);
    }

    [Test]
    public void CycleDoesNotAllocate()
    {
        var stats = NewStats();
        var mortality = new MortalityComponent(stats);
        AllocationAssert.Zero(() =>
        {
            stats.Open();
            stats.Tick(0.1f, 0.01f, 1f, 0.5f, 5f, 12f, 5f, out _);
            stats.Commit();
            stats.En = 25f;
            stats.Hp = 50f;
        }, 200, "StatsComponent.Open/Tick/Commit");
        Assert.IsFalse(mortality.IsDead);
    }
}
