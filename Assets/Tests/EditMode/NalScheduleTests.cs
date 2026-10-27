using NUnit.Framework;
using TheRavine.Generator;

public class NalScheduleTests
{
    [Test]
    public void PopsInAscendingDueOrder()
    {
        var schedule = new NalSchedule(8);
        double[] dues = { 5d, 1d, 4d, 2d, 3d, 0.5d, 9d };
        for (int i = 0; i < dues.Length; i++) schedule.Push(dues[i], i, 0);

        double previous = double.NegativeInfinity;
        int popped = 0;
        while (schedule.TryPopDue(100d, out NalSchedule.Entry entry))
        {
            Assert.GreaterOrEqual(entry.Due, previous);
            previous = entry.Due;
            popped++;
        }

        Assert.AreEqual(dues.Length, popped);
        Assert.AreEqual(0, schedule.Count);
    }

    [Test]
    public void EntryKeepsPosAndPrefab()
    {
        var schedule = new NalSchedule(4);
        schedule.Push(1d, 42L, 7);

        Assert.IsTrue(schedule.TryPopDue(1d, out NalSchedule.Entry entry));
        Assert.AreEqual(42L, entry.Pos);
        Assert.AreEqual(7, entry.PrefabId);
        Assert.AreEqual(1d, entry.Due);
    }

    [Test]
    public void FutureEntriesAreNotReturned()
    {
        var schedule = new NalSchedule(4);
        schedule.Push(10d, 1L, 0);
        schedule.Push(2d, 2L, 0);

        Assert.IsFalse(schedule.TryPopDue(1d, out _));
        Assert.AreEqual(2, schedule.Count);

        Assert.IsTrue(schedule.TryPopDue(2d, out NalSchedule.Entry entry));
        Assert.AreEqual(2L, entry.Pos);
        Assert.IsFalse(schedule.TryPopDue(9.9d, out _));
        Assert.IsTrue(schedule.TryPopDue(10d, out entry));
        Assert.AreEqual(1L, entry.Pos);
    }

    [Test]
    public void EmptyScheduleReturnsNothing()
    {
        var schedule = new NalSchedule(4);

        Assert.IsFalse(schedule.TryPopDue(double.MaxValue, out NalSchedule.Entry entry));
        Assert.AreEqual(0L, entry.Pos);
    }

    [Test]
    public void ClearResetsCount()
    {
        var schedule = new NalSchedule(4);
        for (int i = 0; i < 3; i++) schedule.Push(i, i, 0);

        schedule.Clear();

        Assert.AreEqual(0, schedule.Count);
        Assert.IsFalse(schedule.TryPopDue(double.MaxValue, out _));
    }

    [Test]
    public void GrowthKeepsAllEntriesInOrder()
    {
        var schedule = new NalSchedule(4);
        const int total = 1000;
        for (int i = 0; i < total; i++) schedule.Push((i * 37) % total, i, i);

        Assert.AreEqual(total, schedule.Count);
        Assert.GreaterOrEqual(schedule.Capacity, total);

        double previous = double.NegativeInfinity;
        int popped = 0;
        while (schedule.TryPopDue(double.MaxValue, out NalSchedule.Entry entry))
        {
            Assert.GreaterOrEqual(entry.Due, previous);
            previous = entry.Due;
            popped++;
        }

        Assert.AreEqual(total, popped);
    }

    [Test]
    public void EqualDueEntriesAreAllReturned()
    {
        var schedule = new NalSchedule(4);
        for (int i = 0; i < 5; i++) schedule.Push(3d, i, 0);

        int popped = 0;
        while (schedule.TryPopDue(3d, out _)) popped++;

        Assert.AreEqual(5, popped);
    }

    [Test]
    public void CountDueCountsOnlyReadyEntries()
    {
        var schedule = new NalSchedule(4);
        for (int i = 0; i < 100; i++) schedule.Push(i, i, 0);

        Assert.AreEqual(0, schedule.CountDue(-1d));
        Assert.AreEqual(1, schedule.CountDue(0d));
        Assert.AreEqual(50, schedule.CountDue(49d));
        Assert.AreEqual(100, schedule.CountDue(1000d));
    }

    [Test]
    public void PushAndPopDoNotAllocate()
    {
        var schedule = new NalSchedule(64);
        for (int i = 0; i < 32; i++) schedule.Push(i, i, 0);

        double due = 100d;
        AllocationAssert.Zero(() =>
        {
            schedule.Push(due, 1L, 1);
            schedule.TryPopDue(due, out _);
        }, 100, "NalSchedule.Push/TryPopDue");
    }
}
