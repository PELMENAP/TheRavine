using NUnit.Framework;
using Unity.Mathematics;

public class HelpLogTests
{
    private const float Tau = 300f;

    [Test]
    public void BalanceIsReceivedMinusGivenAndDecays()
    {
        var log = new HelpLog(2, 4);
        try
        {
            log.NoteReceived(0, 7, 10f, 0f, Tau);
            log.NoteGiven(0, 7, 4f, 0f, Tau);
            Assert.AreEqual(6f, log.Balance(0, 7, 0f, Tau), 1e-5f);

            float later = log.Balance(0, 7, Tau, Tau);
            Assert.AreEqual(6f * math.exp(-1f), later, 1e-4f);
            Assert.AreEqual(0f, log.Balance(0, 99, 0f, Tau));
            Assert.AreEqual(0f, log.Balance(1, 7, 0f, Tau));
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void FullLogReplacesTheOldestMeeting()
    {
        var log = new HelpLog(1, 3);
        try
        {
            log.NoteReceived(0, 1, 1f, 10f, Tau);
            log.NoteReceived(0, 2, 1f, 20f, Tau);
            log.NoteReceived(0, 3, 1f, 30f, Tau);
            log.NoteGiven(0, 1, 1f, 40f, Tau);
            log.NoteReceived(0, 4, 1f, 50f, Tau);

            Assert.AreEqual(3, log.Count(0));
            Assert.GreaterOrEqual(log.Find(0, 1), 0);
            Assert.GreaterOrEqual(log.Find(0, 3), 0);
            Assert.GreaterOrEqual(log.Find(0, 4), 0);
            Assert.AreEqual(-1, log.Find(0, 2));
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void PlayerPartnersUseNegativeIdsLikeAnyOther()
    {
        var log = new HelpLog(1, 2);
        try
        {
            int player = CodexAuthor.ForPlayer(3UL);
            log.NoteReceived(0, player, 5f, 0f, Tau);
            Assert.Less(player, 0);
            Assert.AreEqual(5f, log.Balance(0, player, 0f, Tau), 1e-5f);
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void InvalidNotesAreIgnored()
    {
        var log = new HelpLog(1, 2);
        try
        {
            log.NoteReceived(0, 0, 5f, 0f, Tau);
            log.NoteReceived(0, 3, float.NaN, 0f, Tau);
            log.NoteGiven(0, 3, -1f, 0f, Tau);
            log.NoteReceived(5, 3, 1f, 0f, Tau);
            Assert.AreEqual(0, log.Count(0));
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void SwapBackMovesTheLastRowAndClearsIt()
    {
        var log = new HelpLog(4, 3);
        try
        {
            for (int i = 0; i < 4; i++) log.NoteReceived(i, 100 + i, i + 1f, 0f, Tau);
            log.RemoveSwapBack(1, 3);

            Assert.AreEqual(1f, log.Balance(0, 100, 0f, Tau), 1e-5f);
            Assert.AreEqual(4f, log.Balance(1, 103, 0f, Tau), 1e-5f);
            Assert.AreEqual(0f, log.Balance(1, 101, 0f, Tau));
            Assert.AreEqual(3f, log.Balance(2, 102, 0f, Tau), 1e-5f);
            Assert.AreEqual(0, log.Count(3));

            log.RemoveSwapBack(2, 2);
            Assert.AreEqual(0, log.Count(2));
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void RestoreKeepsTheEntryAndSkipsAFullRow()
    {
        var log = new HelpLog(1, 1);
        try
        {
            var entry = new HelpEntry
            {
                Partner = 9, Balance = 2f, ComfortAccum = 0.2f, BalanceTime = 1f, ComfortTime = HelpLog.Never,
                GaveTime = HelpLog.Never, ReceivedTime = 1f, MetTime = 1f,
            };
            log.Restore(0, in entry);
            Assert.AreEqual(2f, log.Balance(0, 9, 1f, Tau), 1e-5f);
            Assert.IsTrue(log.ComfortReady(0, 9, 5f, 20f));

            var other = entry;
            other.Partner = 10;
            log.Restore(0, in other);
            Assert.AreEqual(1, log.Count(0));
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void NotesDoNotAllocate()
    {
        var log = new HelpLog(4, 4);
        try
        {
            float t = 0f;
            float sink = 0f;
            TestDelegate step = () =>
            {
                t += 1f;
                log.NoteReceived(1, (int)t % 9 + 1, 1f, t, Tau);
                log.NoteGiven(1, (int)t % 7 + 1, 1f, t, Tau);
                log.NoteComfort(1, 3, 0.1f, t, 120f);
                sink += log.Balance(1, 3, t, Tau) + log.ComfortAccum(1, 3, t, 120f);
                if (log.ComfortReady(1, 3, t, 20f)) sink += 1f;
            };
            AllocationAssert.Zero(step, 200, "HelpLog notes");
        }
        finally
        {
            log.Dispose();
        }
    }
}
