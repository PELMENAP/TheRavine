using NUnit.Framework;
using Unity.Mathematics;

public class HotPathAllocationTests
{
    [Test]
    public void SpeechEncodeIsCached()
    {
        var a = new float4(0.25f, -0.5f, 1f, 0f);
        string first = SpeechComponent.Encode(a);
        Assert.AreSame(first, SpeechComponent.Encode(a));
        Assert.AreEqual(8, first.Length);

        var b = new float4(-1f, 1f, -1f, 1f);
        Assert.AreNotEqual(first, SpeechComponent.Encode(b));
        Assert.AreEqual(first, SpeechComponent.Encode(a));
    }

    [Test]
    public void SpeechEncodeWarmDoesNotAllocate()
    {
        var s0 = new float4(1f, 0f, 0f, 0f);
        var s1 = new float4(0f, 1f, 0f, 0f);
        SpeechComponent.Encode(s0);
        SpeechComponent.Encode(s1);
        AllocationAssert.Zero(() =>
        {
            SpeechComponent.Encode(s0);
            SpeechComponent.Encode(s1);
        }, 200, "SpeechComponent.Encode");
    }

    [Test]
    public void TickBudgetDoesNotAllocate()
    {
        var budget = new TickWindowBudget();
        int cursor = 0;
        AllocationAssert.Zero(() =>
        {
            cursor += budget.Take(1f / 64f, 1f, 200, 200 - cursor);
            if (cursor >= 200) cursor = 0;
        }, 500, "TickWindowBudget.Take");
    }

    [Test]
    public void TerminalPenaltyDoesNotAllocate()
    {
        float sum = 0f;
        AllocationAssert.Zero(() =>
        {
            for (int i = 0; i < 16; i++)
                sum += TerminalPenaltyMath.Compute(-1f, true, i * 0.1f - 0.8f, 0.5f, 0.25f, 2f);
        }, 200, "TerminalPenaltyMath.Compute");
    }
}
