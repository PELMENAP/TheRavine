using NUnit.Framework;

public class CorpseBiteTests
{
    [Test]
    public void FullPortionsWhileCorpseStaysAndSubMinimumLeftoverIsDropped()
    {
        float left      = 31f;
        float delivered = 0f;
        int   bites     = 0;

        while (true)
        {
            var bite = CorpseBite.Plan(true, left, false, 6f, 2f);
            delivered += bite.Energy;
            bites++;
            if (!bite.Stays) break;

            Assert.AreEqual(6f, bite.Energy);
            left = bite.Remaining;
        }

        Assert.AreEqual(5, bites);
        Assert.AreEqual(30f, delivered);
    }

    [Test]
    public void InfectionIsReportedOnceAcrossBites()
    {
        float left    = 31f;
        bool  infected = true;
        int   reported = 0;

        while (true)
        {
            var bite = CorpseBite.Plan(true, left, infected, 6f, 2f);
            if (bite.Infected) reported++;
            if (!bite.Stays) break;

            left     = bite.Remaining;
            infected = false;
        }

        Assert.AreEqual(1, reported);
    }

    [Test]
    public void InfectionSurvivesWhenFinalBiteRemovesCorpse()
    {
        var bite = CorpseBite.Plan(true, 5f, true, 6f, 2f);

        Assert.AreEqual(5f, bite.Energy);
        Assert.IsTrue(bite.Infected);
        Assert.IsFalse(bite.Stays);
    }

    [Test]
    public void CorpseWithoutRecordGivesNothingAndIsRemoved()
    {
        var bite = CorpseBite.Plan(false, 20f, true, 6f, 2f);

        Assert.AreEqual(0f, bite.Energy);
        Assert.IsFalse(bite.Infected);
        Assert.IsFalse(bite.Stays);
    }

    [Test]
    public void EmptyCorpseGivesNothingAndIsRemoved()
    {
        var bite = CorpseBite.Plan(true, 0f, false, 6f, 2f);

        Assert.AreEqual(0f, bite.Energy);
        Assert.IsFalse(bite.Stays);
    }
}
