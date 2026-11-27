using NUnit.Framework;
using Unity.Mathematics;

public class SocialReliefTests
{
    private const float Cap = 0.6f;
    private const float Window = 120f;
    private const float Cooldown = 20f;
    private const float Tau = 300f;

    [Test]
    public void ComfortReliefIsProportionalToStressAndShrinksWithTheAccumulator()
    {
        float first = SocialReliefMath.ComfortRelief(0.8f, 0f, 0.5f, Cap);
        float later = SocialReliefMath.ComfortRelief(0.8f, 0.3f, 0.5f, Cap);
        Assert.AreEqual(0.4f, first, 1e-6f);
        Assert.Less(later, first);
        Assert.AreEqual(0f, SocialReliefMath.ComfortRelief(0.8f, Cap, 0.5f, Cap));
        Assert.AreEqual(0f, SocialReliefMath.ComfortRelief(0f, 0f, 0.5f, Cap));
        Assert.AreEqual(0f, SocialReliefMath.ComfortRelief(0.8f, 0f, 0.5f, 0f));
        Assert.LessOrEqual(SocialReliefMath.ComfortRelief(0.1f, 0f, 1f, Cap), 0.1f);
    }

    [Test]
    public void TwoAgentsComfortingInALoopStayUnderTheCap()
    {
        var log = new HelpLog(2, 4);
        var detector = new AnomalyDetector(64, 30f, 0.3f, 1e-3f);
        try
        {
            float stressA = 0.9f, stressB = 0.9f;
            var relieved = new System.Collections.Generic.List<(float time, float amount)>();
            float total = 0f;

            for (float now = 0f; now < 900f; now += 1f)
            {
                stressA = math.min(1f, stressA + 0.05f);
                stressB = math.min(1f, stressB + 0.05f);

                for (int turn = 0; turn < 2; turn++)
                {
                    int giver = turn, partner = 1 - turn;
                    int giverId = giver + 1, partnerId = partner + 1;
                    float partnerStress = partner == 0 ? stressA : stressB;
                    if (partnerStress < 0.25f || !log.ComfortReady(giver, partnerId, now, Cooldown)) continue;

                    float accum = log.ComfortAccum(giver, partnerId, now, Window);
                    float relief = math.min(SocialReliefMath.ComfortRelief(partnerStress, accum, 0.5f, Cap), partnerStress);
                    if (partner == 0) stressA -= relief; else stressB -= relief;
                    log.NoteComfort(giver, partnerId, relief, now, Window);
                    log.NoteComfort(partner, giverId, relief, now, Window);
                    detector.NoteRelief(giverId, partnerId, relief);
                    relieved.Add((now, relief));
                    total += relief;
                }
            }

            Assert.Greater(total, 0f);
            for (int i = 0; i < relieved.Count; i++)
            {
                float windowSum = 0f;
                for (int j = 0; j <= i; j++)
                    if (relieved[i].time - relieved[j].time <= Window) windowSum += relieved[j].amount * math.exp(-(relieved[i].time - relieved[j].time) / Window);
                Assert.LessOrEqual(windowSum, Cap + 1e-4f, $"event {i}");
            }

            Assert.Greater(total, Cap, "the loop must be long enough to hit the cap");
            detector.ConsumeWindow(out _, out int farm);
            Assert.GreaterOrEqual(farm, 1);
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void PairCooldownBlocksAnImmediateRepeatInBothDirections()
    {
        var log = new HelpLog(2, 4);
        try
        {
            Assert.IsTrue(log.ComfortReady(0, 2, 10f, Cooldown));
            log.NoteComfort(0, 2, 0.1f, 10f, Window);
            log.NoteComfort(1, 1, 0.1f, 10f, Window);
            Assert.IsFalse(log.ComfortReady(0, 2, 15f, Cooldown));
            Assert.IsFalse(log.ComfortReady(1, 1, 15f, Cooldown));
            Assert.IsTrue(log.ComfortReady(0, 2, 31f, Cooldown));
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void ReciprocalFeedingGivesNoRelief()
    {
        var log = new HelpLog(2, 4);
        try
        {
            float need = 40f;
            Assert.IsFalse(log.ReceivedWithin(0, 2, 0f, 60f));
            float first = SocialReliefMath.FeedRelief(20f, need, 0.8f, 0.5f, false);
            Assert.Greater(first, 0f);

            log.NoteGiven(0, 2, 20f, 0f, Tau);
            log.NoteReceived(1, 1, 20f, 0f, Tau);

            bool reciprocal = log.ReceivedWithin(1, 1, 5f, 60f) || log.GaveWithin(0, 2, 5f, 60f);
            Assert.IsTrue(log.GaveWithin(0, 2, 5f, 60f));
            Assert.IsTrue(log.ReceivedWithin(1, 1, 5f, 60f));
            Assert.IsTrue(reciprocal);
            Assert.AreEqual(0f, SocialReliefMath.FeedRelief(20f, need, 0.8f, 0.5f, reciprocal));
            Assert.IsFalse(log.GaveWithin(0, 2, 100f, 60f));
        }
        finally
        {
            log.Dispose();
        }
    }

    [Test]
    public void FeedReliefFollowsTheClosedShareOfNeed()
    {
        Assert.AreEqual(0.5f * 0.5f * 0.8f, SocialReliefMath.FeedRelief(20f, 40f, 0.8f, 0.5f, false), 1e-6f);
        Assert.AreEqual(0.5f * 0.8f, SocialReliefMath.FeedRelief(80f, 40f, 0.8f, 0.5f, false), 1e-6f);
        Assert.AreEqual(0f, SocialReliefMath.FeedRelief(0f, 40f, 0.8f, 0.5f, false));
        Assert.AreEqual(0f, SocialReliefMath.FeedRelief(10f, 0f, 0.8f, 0.5f, false));
    }

    [Test]
    public void TransferLossIsBookedAndTheLedgerResidualStaysZero()
    {
        var ledger = new EnergyLedger();
        float giver = 100f, receiver = 20f;
        ledger.Sample(giver + receiver, 0f, 0f, 0f, 0f, 0f);

        float portion = 30f;
        giver -= portion;
        receiver += SocialReliefMath.Delivered(portion, 0.1f, out float lost);
        ledger.Flows.Social += lost;
        ledger.Sample(giver + receiver, 0f, 0f, 0f, 0f, 0f);

        Assert.AreEqual(3f, lost, 1e-5f);
        Assert.AreEqual(3f, ledger.Last.Social, 1e-5f);
        Assert.AreEqual(0f, ledger.Residual, 1e-4f);
        Assert.AreEqual(3f, ledger.Last.Outflow, 1e-5f);

        ledger.Sample(giver + receiver, 0f, 0f, 0f, 0f, 0f);
        float cost = 2f;
        giver -= cost;
        ledger.Flows.Social += cost;
        ledger.Sample(giver + receiver, 0f, 0f, 0f, 0f, 0f);
        Assert.AreEqual(0f, ledger.Residual, 1e-4f);
    }

    [Test]
    public void NoLossMeansTheWholePortionIsDelivered()
    {
        Assert.AreEqual(30f, SocialReliefMath.Delivered(30f, 0f, out float lost));
        Assert.AreEqual(0f, lost);
    }

    [Test]
    public void KinStressedHintOpensGiveInTheCourtPlan()
    {
        int give = 1 << (int)EntityAction.Give;
        Assert.AreEqual(0, PlanCatalog.EntryMask(PlanKind.Court, PlanHint.None) & give);
        Assert.AreNotEqual(0, PlanCatalog.EntryMask(PlanKind.Court, PlanHint.KinStressed) & give);
    }

    [Test]
    public void RemainderThatDoesNotFitTheGiversStomachIsReturnedNotLost()
    {
        var stomach = new DigestionComponent();
        stomach.Configure(5f);

        float overflow = SocialReliefMath.ReturnToGiver(stomach, 10f);
        Assert.AreEqual(5f, stomach.Stomach, 1e-6f);
        Assert.AreEqual(5f, overflow, 1e-6f);
        Assert.AreEqual(10f, stomach.Stomach + overflow, 1e-6f);

        Assert.AreEqual(7f, SocialReliefMath.ReturnToGiver(stomach, 7f), 1e-6f);
        Assert.AreEqual(0f, SocialReliefMath.ReturnToGiver(stomach, 0f));
    }

    [Test]
    public void RemainderThatFitsLeavesNothingToCarry()
    {
        var stomach = new DigestionComponent();
        stomach.Configure(20f);
        Assert.AreEqual(0f, SocialReliefMath.ReturnToGiver(stomach, 12f), 1e-6f);
        Assert.AreEqual(12f, stomach.Stomach, 1e-6f);
    }
}
