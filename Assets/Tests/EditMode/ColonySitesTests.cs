using NUnit.Framework;
using Unity.Mathematics;

public class ColonySitesTests
{
    [Test]
    public void SitesKeepTheSpacingFromExistingAndNewNests()
    {
        var sites = new float2[20];
        sites[0] = float2.zero;
        int placed = ColonySites.Choose(sites, 1, 19, float2.zero, 800f, 40f, 64, _ => 1f);

        Assert.AreEqual(19, placed);
        for (int i = 0; i < 20; i++)
            for (int j = i + 1; j < 20; j++)
                Assert.GreaterOrEqual(math.distance(sites[i], sites[j]), 40f);
    }

    [Test]
    public void BestScoringCandidateWinsAndUnsuitableOnesAreSkipped()
    {
        var sites = new float2[3];
        int placed = ColonySites.Choose(sites, 0, 3, float2.zero, 100f, 0f, 256, p => p.x < 0f ? ColonySites.Unsuitable : p.x + 1f);

        Assert.AreEqual(3, placed);
        for (int i = 0; i < 3; i++) Assert.GreaterOrEqual(sites[i].x, 0f);
    }

    [Test]
    public void UnknownTerrainIsAcceptedOnlyWithoutAScoredCandidate()
    {
        var sites = new float2[2];
        int placed = ColonySites.Choose(sites, 0, 2, float2.zero, 50f, 0f, 16, _ => 0f);
        Assert.AreEqual(2, placed);

        placed = ColonySites.Choose(sites, 0, 2, float2.zero, 50f, 0f, 16, _ => ColonySites.Unsuitable);
        Assert.AreEqual(0, placed);
    }

    [Test]
    public void FewerSitesAreReturnedWhenTheSpacingLeavesNoRoom()
    {
        var sites = new float2[4];
        sites[0] = float2.zero;
        int placed = ColonySites.Choose(sites, 1, 3, float2.zero, 10f, 100f, 32, _ => 1f);
        Assert.AreEqual(0, placed);
    }
}
