using NUnit.Framework;

public class DecisionRingTests
{
    [TestCase(8)]
    [TestCase(6)]
    public void PushWrapsAndDropsOldest(int capacity)
    {
        var ring = new DecisionRing(capacity, 4, 3);
        for (int id = 1; id <= capacity + 3; id++) ring.Push(id).StartTime = id;

        Assert.AreEqual(capacity, ring.Count);
        Assert.AreEqual(4, ring.Oldest.DecisionId);
        Assert.AreEqual(capacity + 3, ring.Newest.DecisionId);
        for (int i = 0; i < ring.Count; i++) Assert.AreEqual(4 + i, ring[i].DecisionId);

        Assert.IsNull(ring.Find(1));
        Assert.IsNull(ring.Find(3));
        Assert.AreEqual(capacity + 1, ring.Find(capacity + 1).DecisionId);
    }

    [TestCase(8)]
    [TestCase(6)]
    public void PopOldestAndClear(int capacity)
    {
        var ring = new DecisionRing(capacity, 4, 3);
        for (int id = 1; id <= 3; id++) ring.Push(id);

        ring.PopOldest();
        Assert.AreEqual(2, ring.Count);
        Assert.AreEqual(2, ring.Oldest.DecisionId);
        Assert.IsNull(ring.Find(1));

        ring.Clear();
        Assert.AreEqual(0, ring.Count);
        Assert.IsNull(ring.Oldest);
        Assert.IsNull(ring.Newest);
        Assert.IsNull(ring.Find(2));
        ring.PopOldest();
        Assert.AreEqual(0, ring.Count);
    }

    [Test]
    public void PushResetsTrainedAndRewardFlags()
    {
        var ring = new DecisionRing(2, 4, 3);
        var first = ring.Push(1);
        first.Trained       = true;
        first.RewardApplied = true;
        first.Evaluation    = -3f;
        first.Predicted     = 2;
        ring.Push(2);

        var reused = ring.Push(3);
        Assert.AreSame(first, reused);
        Assert.IsFalse(reused.Trained);
        Assert.IsFalse(reused.RewardApplied);
        Assert.AreEqual(0f, reused.Evaluation);
        Assert.AreEqual(-1, reused.Predicted);
        Assert.AreEqual(3, reused.DecisionId);
    }

    [Test]
    public void TrainedFlagSurvivesUntilSlotReuse()
    {
        var ring = new DecisionRing(4, 4, 3);
        ring.Push(1).Trained = true;
        ring.Push(2);
        Assert.IsTrue(ring.Find(1).Trained);
        Assert.IsFalse(ring.Find(2).Trained);
    }

    [Test]
    public void DiscountTimeIsExponentialInSeconds()
    {
        Assert.AreEqual(1f, DelayedPerceptron.DiscountTime(0.9f, 0f));
        Assert.AreEqual(1f, DelayedPerceptron.DiscountTime(0.9f, -2f));
        Assert.AreEqual(0.9f, DelayedPerceptron.DiscountTime(0.9f, 1f), 1e-6f);
        Assert.AreEqual(0.81f, DelayedPerceptron.DiscountTime(0.9f, 2f), 1e-6f);
        Assert.AreEqual(1f, DelayedPerceptron.DiscountTime(1f, 100f), 1e-6f);

        float prev = 1f;
        for (int i = 1; i <= 20; i++)
        {
            float d = DelayedPerceptron.DiscountTime(0.95f, i * 0.5f);
            Assert.Less(d, prev);
            Assert.Greater(d, 0f);
            prev = d;
        }
    }

    [Test]
    public void RingOperationsDoNotAllocate()
    {
        var ring = new DecisionRing(16, 8, 4);
        int id = 0;
        AllocationAssert.Zero(() =>
        {
            for (int i = 0; i < 32; i++)
            {
                ring.Push(++id);
                ring.Find(id - 3);
                if ((i & 3) == 0) ring.PopOldest();
            }
            DelayedPerceptron.TerminalReward(0f, -1f, 0.95f, 2f);
        }, 100, "DecisionRing.Push/Find/PopOldest");
    }
}
