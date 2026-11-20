using NUnit.Framework;

public class NoveltyProgressTests
{
    private const float Fast = 0.2f;
    private const float Slow = 0.02f;

    [Test]
    public void RewardIsNeverNegative()
    {
        var system = new NoveltyProgressSystem(2);
        try
        {
            var rng = new System.Random(4);
            for (int i = 0; i < 5000; i++)
            {
                float error = (float)(rng.NextDouble() * (i % 700 < 350 ? 2.0 : 0.1));
                Assert.GreaterOrEqual(system.Step(0, error, Fast, Slow), 0f);
            }
            Assert.AreEqual(0f, system.Step(1, float.NaN, Fast, Slow));
            Assert.AreEqual(0f, system.Step(99, 1f, Fast, Slow));
        }
        finally { system.Dispose(); }
    }

    [Test]
    public void ProgressPaysAndThenDecaysWhenErrorStopsFalling()
    {
        var system = new NoveltyProgressSystem(1);
        try
        {
            for (int i = 0; i < 300; i++) system.Step(0, 1f, Fast, Slow);

            float first = system.Step(0, 0.2f, Fast, Slow);
            Assert.Greater(first, 0f);

            float peak = first;
            for (int i = 0; i < 40; i++) peak = System.Math.Max(peak, system.Step(0, 0.2f, Fast, Slow));
            Assert.Greater(peak, 0.05f);

            float last = 0f;
            for (int i = 0; i < 600; i++) last = system.Step(0, 0.2f, Fast, Slow);
            Assert.Less(last, 0.02f * peak);
        }
        finally { system.Dispose(); }
    }

    [Test]
    public void ErrorThatNeverFallsGivesNothing()
    {
        var system = new NoveltyProgressSystem(1);
        try
        {
            var rng = new System.Random(8);
            for (int i = 0; i < 300; i++) system.Step(0, 0.1f, Fast, Slow);

            float sum = 0f;
            for (int i = 0; i < 400; i++) sum += system.Step(0, 1f, Fast, Slow);
            Assert.AreEqual(0f, sum);

            float noise = 0f;
            for (int i = 0; i < 4000; i++)
            {
                float reward = system.Step(0, 1f + 0.5f * (float)rng.NextDouble(), Fast, Slow);
                if (i >= 2000) noise += reward;
            }
            float jump = 0f;
            var fresh = new NoveltyProgressSystem(1);
            try
            {
                for (int i = 0; i < 300; i++) fresh.Step(0, 1f, Fast, Slow);
                for (int i = 0; i < 40; i++) jump += fresh.Step(0, 0.2f, Fast, Slow);
            }
            finally { fresh.Dispose(); }
            Assert.Less(noise / 2000f, jump / 40f);
        }
        finally { system.Dispose(); }
    }

    [Test]
    public void StateFollowsSwapBackAndResets()
    {
        var system = new NoveltyProgressSystem(3);
        try
        {
            for (int i = 0; i < 3; i++)
            {
                system.Reset(i);
                system.Step(i, 1f + i, Fast, Slow);
                system.Step(i, 2f + i, Fast, Slow);
            }
            float fast2 = system.Fast(2), slow2 = system.Slow(2);

            system.RemoveSwapBack(0, 2);
            Assert.AreEqual(fast2, system.Fast(0));
            Assert.AreEqual(slow2, system.Slow(0));
            Assert.AreEqual(0f, system.Fast(2));
            Assert.AreEqual(0f, system.Slow(2));
            Assert.AreEqual(0f, system.Step(2, 5f, Fast, Slow));

            system.RemoveSwapBack(0, 9);
            Assert.AreEqual(fast2, system.Fast(0));
        }
        finally { system.Dispose(); }
    }
}
