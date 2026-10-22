using NUnit.Framework;
using Unity.Mathematics;

public class SoaSwapBackTests
{
    [SetUp]
    public void SetUp() => SimulationRules.Bind(null);

    [Test]
    public void CasteThresholdsFollowSwapBack()
    {
        var system = new CasteThresholdSystem(4);
        try
        {
            for (int i = 0; i < 4; i++) system.Reset(i, new float4(i));
            system.RemoveSwapBack(1, 3);

            Assert.AreEqual(new float4(0f), system.Get(0));
            Assert.AreEqual(new float4(3f), system.Get(1));
            Assert.AreEqual(new float4(2f), system.Get(2));
            Assert.AreEqual(float4.zero, system.Get(3));

            system.RemoveSwapBack(2, 2);
            Assert.AreEqual(float4.zero, system.Get(2));
        }
        finally
        {
            system.Dispose();
        }
    }

    [Test]
    public void InstinctSlotIsResetAfterSwapBack()
    {
        var system = new InstinctSystem(4);
        try
        {
            for (int i = 0; i < 4; i++) system.Reset(i, default);
            system.RemoveSwapBack(0, 3);

            var freed = system.Output(3);
            Assert.AreEqual(InstinctBits.NoReflex, freed.Reflex);
            Assert.AreEqual(1, freed.NeedsDecision);
        }
        finally
        {
            system.Dispose();
        }
    }

    [Test]
    public void OutOfRangeSwapBackIsIgnored()
    {
        var system = new CasteThresholdSystem(2);
        try
        {
            system.Reset(0, new float4(5f));
            system.RemoveSwapBack(0, 7);
            Assert.AreEqual(new float4(5f), system.Get(0));
        }
        finally
        {
            system.Dispose();
        }
    }
}
