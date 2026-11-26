using NUnit.Framework;

public class AnomalyDetectorTests
{
    private static AnomalyDetector Create() => new AnomalyDetector(64, 30f, 1f, 1e-3f);

    [Test]
    public void ReturnTransferWithinTheWindowIsOneLoop()
    {
        var d = Create();
        d.NoteTransfer(1, 2, 10f, 0d);
        d.NoteTransfer(2, 1, 10f, 5d);

        d.ConsumeWindow(out int loops, out int farm);
        Assert.AreEqual(1, loops);
        Assert.AreEqual(0, farm);
    }

    [Test]
    public void OneWayTransfersAreNotLoops()
    {
        var d = Create();
        d.NoteTransfer(1, 2, 10f, 0d);
        d.NoteTransfer(1, 2, 10f, 1d);
        d.NoteTransfer(2, 3, 10f, 2d);

        d.ConsumeWindow(out int loops, out _);
        Assert.AreEqual(0, loops);
    }

    [Test]
    public void PartialReturnWithPositiveNetTransferIsNotALoop()
    {
        var d = Create();
        d.NoteTransfer(1, 2, 10f, 0d);
        d.NoteTransfer(2, 1, 4f, 1d);

        d.ConsumeWindow(out int loops, out _);
        Assert.AreEqual(0, loops);
    }

    [Test]
    public void PartialReturnsThatAddUpToTheGiftAreALoop()
    {
        var d = Create();
        d.NoteTransfer(1, 2, 10f, 0d);
        d.NoteTransfer(2, 1, 4f, 1d);
        d.NoteTransfer(2, 1, 6f, 2d);

        d.ConsumeWindow(out int loops, out _);
        Assert.AreEqual(1, loops);
    }

    [Test]
    public void ReturnAfterTheWindowIsNotALoop()
    {
        var d = Create();
        d.NoteTransfer(1, 2, 10f, 0d);
        d.NoteTransfer(2, 1, 10f, 31d);

        d.ConsumeWindow(out int loops, out _);
        Assert.AreEqual(0, loops);
    }

    [Test]
    public void WindowConsumptionResetsTheCounters()
    {
        var d = Create();
        d.NoteTransfer(1, 2, 1f, 0d);
        d.NoteTransfer(2, 1, 1f, 1d);
        d.NoteRelief(1, 2, 5f);
        d.ConsumeWindow(out _, out _);

        d.ConsumeWindow(out int loops, out int farm);
        Assert.AreEqual(0, loops);
        Assert.AreEqual(0, farm);
    }

    [Test]
    public void ReliefAboveTheThresholdIsCountedOncePerPair()
    {
        var d = Create();
        d.NoteRelief(1, 2, 0.6f);
        d.NoteRelief(1, 2, 0.6f);
        d.NoteRelief(1, 2, 0.6f);
        d.NoteRelief(2, 1, 0.6f);

        d.ConsumeWindow(out _, out int farm);
        Assert.AreEqual(1, farm);
    }

    [Test]
    public void StorageStaysBoundedUnderManyPairs()
    {
        var d = new AnomalyDetector(16, 30f, 1f, 1e-3f);
        for (int i = 0; i < 1000; i++)
        {
            d.NoteRelief(i, i + 1, 0.1f);
            d.NoteInvestment(i, i, 1f);
            d.NoteTransfer(i, i + 1, 1f, i);
        }
        Assert.Greater(d.DroppedPairs, 0);
        Assert.Greater(d.DroppedVotes, 0);
    }

    [Test]
    public void ConcentrationIsTheHerfindahlIndexPerLaw()
    {
        var d = Create();
        d.NoteInvestment(1, 10, 6f);
        d.NoteInvestment(1, 11, 4f);
        d.NoteInvestment(2, 10, 3f);

        float value = d.SampleConcentration(null, out int laws);
        Assert.AreEqual(2, laws);
        Assert.AreEqual((0.52f + 1f) * 0.5f, value, 1e-5f);
    }

    [Test]
    public void NotesDoNotAllocate()
    {
        var d = Create();
        double t = 0d;
        TestDelegate step = () =>
        {
            t += 1d;
            d.NoteTransfer(1, 2, 1f, t);
            d.NoteTransfer(2, 1, 1f, t);
            d.NoteRelief(1, 2, 0.01f);
            d.NoteInvestment(1, 2, 1f);
            d.ConsumeWindow(out _, out _);
        };
        AllocationAssert.Zero(step, 200, "AnomalyDetector notes");
    }
}
