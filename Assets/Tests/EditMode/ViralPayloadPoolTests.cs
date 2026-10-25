using NUnit.Framework;
using TheRavine.EntityControl.Virology;

public class ViralPayloadPoolTests
{
    [Test]
    public void SecondReleaseOfSamePayloadDoesNotReturnBufferTwice()
    {
        var payload = new ViralPayload { Codons = ViralPayloadPool.Rent() };
        int before  = ViralPayloadPool.Pooled;

        ViralPayloadPool.Release(ref payload);
        ViralPayloadPool.Release(ref payload);

        Assert.AreEqual(before + 1, ViralPayloadPool.Pooled);
        Assert.IsNull(payload.Codons);
    }
}
