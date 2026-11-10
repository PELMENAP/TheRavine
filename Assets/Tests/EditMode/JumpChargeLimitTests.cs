using NUnit.Framework;
using TheRavine.EntityControl;

public class JumpChargeLimitTests
{
    private const float Max = 1f;
    private const float Slack = 0.2f;

    [Test]
    public void FullChargeWithoutHoldingIsCappedByElapsedTime()
    {
        Assert.AreEqual(0.2f, JumpChargeLimit.Clamp(1f, 0.0, Max, Slack), 1e-5f);
        Assert.AreEqual(0.45f, JumpChargeLimit.Clamp(1f, 0.25, Max, Slack), 1e-5f);
        Assert.AreEqual(1f, JumpChargeLimit.Clamp(1f, 0.8, Max, Slack), 1e-5f);
    }

    [Test]
    public void HonestChargeIsKept()
    {
        Assert.AreEqual(0.5f, JumpChargeLimit.Clamp(0.5f, 3.0, Max, Slack), 1e-5f);
        Assert.AreEqual(0.1f, JumpChargeLimit.Clamp(0.1f, 0.5, Max, Slack), 1e-5f);
        Assert.AreEqual(0f, JumpChargeLimit.Clamp(0f, 10.0, Max, Slack), 1e-5f);
    }

    [Test]
    public void RepeatedMaximumChargeNeverProducesAMaximumJump()
    {
        double now = 0.0;
        double last = 0.0;

        for (int i = 0; i < 100; i++)
        {
            now += 0.1;
            float charge = JumpChargeLimit.Clamp(1f, now - last, Max, Slack);
            last = now;
            Assert.Less(charge, 0.5f, $"jump {i}");
        }
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void NonFiniteChargeIsZero(float charge)
    {
        Assert.AreEqual(0f, JumpChargeLimit.Clamp(charge, 100.0, Max, Slack));
    }

    [TestCase(-5f)]
    [TestCase(5f)]
    [TestCase(float.MaxValue)]
    [TestCase(float.MinValue)]
    public void OutOfRangeChargeIsSaturated(float charge)
    {
        float result = JumpChargeLimit.Clamp(charge, 100.0, Max, Slack);

        Assert.GreaterOrEqual(result, 0f);
        Assert.LessOrEqual(result, 1f);
    }

    [Test]
    public void NegativeOrHugeElapsedTimeAndBadSlackStayInRange()
    {
        Assert.AreEqual(0.2f, JumpChargeLimit.Clamp(1f, -100.0, Max, Slack), 1e-5f);
        Assert.AreEqual(1f, JumpChargeLimit.Clamp(1f, double.MaxValue, Max, Slack), 1e-5f);
        Assert.AreEqual(0f, JumpChargeLimit.Clamp(1f, 0.0, Max, -5f), 1e-5f);
        Assert.AreEqual(0f, JumpChargeLimit.Clamp(1f, 1.0, 0f, Slack));
    }
}
