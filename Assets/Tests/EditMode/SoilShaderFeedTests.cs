using NUnit.Framework;

public class SoilShaderFeedTests
{
    [Test]
    public void UnknownChunkHasNoSoil()
    {
        var index = new ChunkFoodIndex(null, null);

        bool known = index.TryGetSoil(0L, out float fertility, out float depletion, out float energy);

        Assert.IsFalse(known);
        Assert.AreEqual(0f, fertility);
        Assert.AreEqual(0f, depletion);
        Assert.AreEqual(0f, energy);
    }

    [Test]
    public void PublishDoesNotAllocate()
    {
        var index = new ChunkFoodIndex(null, null);
        var feed = new SoilShaderFeed();
        try
        {
            AllocationAssert.Zero(() => feed.Publish(index, 3, -2), 64, "SoilShaderFeed.Publish");
        }
        finally
        {
            feed.Dispose();
        }
    }
}
