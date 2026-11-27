using NUnit.Framework;
using Unity.Mathematics;

public class LineageTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = TestRules.Bind();

    [TearDown]
    public void TearDown() => TestRules.Release(_rules);

    [Test]
    public void CrossTakesEachLocusFromOneParent()
    {
        uint a = 0xF0F0F0F0u, b = 0x0FF00FF0u, pick = 0xFFFF0000u;
        Assert.AreEqual((a & pick) | (b & ~pick), MateLoci.Cross(a, b, pick));
        for (int i = 0; i < 200; i++)
        {
            uint c = MateLoci.Cross(a, b);
            Assert.AreEqual(0u, c & ~(a | b));
            Assert.AreEqual(a & b, c & a & b);
        }
    }

    [Test]
    public void PairsNeverJoinALocusWithItself()
    {
        for (int k = 0; k < MateLoci.Count * (MateLoci.Count - 1) / 2; k++)
        {
            int2 p = MateLoci.Pair(k);
            Assert.AreNotEqual(p.x, p.y);
            Assert.That(p.x, Is.InRange(0, MateLoci.Count - 1));
            Assert.That(p.y, Is.InRange(0, MateLoci.Count - 1));
        }
    }

    [Test]
    public void ConflictNeedsBothAllelesOfAPair()
    {
        int2 p = MateLoci.Pair(0);
        uint first = 1u << p.x, second = 1u << p.y;
        Assert.AreEqual(0, MateLoci.Conflicts(first, 1));
        Assert.AreEqual(0, MateLoci.Conflicts(second, 1));
        Assert.AreEqual(1, MateLoci.Conflicts(first | second, 1));
        Assert.AreEqual(0, MateLoci.Conflicts(0u, 24));
        Assert.AreEqual(1f, MateLoci.Viability(0, 0.9f));
        Assert.AreEqual(0.25f, MateLoci.Viability(2, 0.5f), 1e-6f);
    }

    [Test]
    public void HybridOfDivergedLinesCanConflictWhileParentsDoNot()
    {
        int2 p = MateLoci.Pair(3);
        uint lineA = 1u << p.x, lineB = 1u << p.y;
        Assert.AreEqual(0, MateLoci.Conflicts(lineA, 24));
        Assert.AreEqual(0, MateLoci.Conflicts(lineB, 24));
        uint hybrid = MateLoci.Cross(lineA, lineB, lineA);
        Assert.Greater(MateLoci.Conflicts(hybrid, 24), 0);
    }

    [Test]
    public void DivergenceIsZeroForSamePopulationAndOneForOpposite()
    {
        var a = new[] { 0u, 0u, 0u };
        var b = new[] { ~0u, ~0u };
        Assert.AreEqual(0f, MateLoci.Divergence(a, 3, a, 3));
        Assert.AreEqual(1f, MateLoci.Divergence(a, 3, b, 2), 1e-6f);
    }

    [Test]
    public void GeneticDistanceIsNormalised()
    {
        var a = GeneticParameters.Default;
        Assert.AreEqual(0f, GeneticDistance.Between(in a, in a));
        GeneticParameters lo = default, hi = default;
        var sl = GeneticParameters.AsSpan(ref lo);
        var sh = GeneticParameters.AsSpan(ref hi);
        for (int i = 0; i < sl.Length; i++)
        {
            sl[i] = GeneticParameters.ParameterRanges[i].min;
            sh[i] = GeneticParameters.ParameterRanges[i].max;
        }
        Assert.AreEqual(1f, GeneticDistance.Between(in lo, in hi), 1e-5f);
    }

    [Test]
    public void RelatednessMatchesClassicPedigrees()
    {
        var store = new LineageStore(64);
        store.Birth(1, 0, 0, 0f, 0u);
        store.Birth(2, 0, 0, 0f, 0u);
        store.Birth(3, 1, 2, 1f, 0u);
        store.Birth(4, 1, 2, 1f, 0u);
        store.Birth(5, 1, 0, 1f, 0u);
        store.Birth(6, 0, 0, 1f, 0u);
        store.Birth(7, 0, 0, 1f, 0u);
        store.Birth(8, 3, 6, 2f, 0u);
        store.Birth(9, 4, 7, 2f, 0u);

        Assert.AreEqual(1f, store.Relatedness(3, 3, 4));
        Assert.AreEqual(0.5f, store.Relatedness(1, 3, 4), 1e-6f);
        Assert.AreEqual(0.5f, store.Relatedness(3, 4, 4), 1e-6f);
        Assert.AreEqual(0.25f, store.Relatedness(3, 5, 4), 1e-6f);
        Assert.AreEqual(0.125f, store.Relatedness(8, 9, 4), 1e-6f);
        Assert.AreEqual(0f, store.Relatedness(6, 7, 4));
        Assert.AreEqual(0f, store.Relatedness(8, 9, 1));
        Assert.IsTrue(store.TryGet(8, out var rec));
        Assert.AreEqual(2, rec.Generation);
    }

    [Test]
    public void PruneKeepsLivingRecordsAndDropsOldDead()
    {
        var store = new LineageStore(16);
        for (int i = 1; i <= 16; i++) store.Birth(i, 0, 0, i, 0u);
        for (int i = 1; i <= 8; i++) store.Death(i, 20f);
        for (int i = 17; i <= 24; i++) store.Birth(i, 0, 0, i, 0u);
        Assert.LessOrEqual(store.Count, 16);
        for (int i = 9; i <= 24; i++) Assert.IsTrue(store.TryGet(i, out var r) && r.Alive, $"living {i}");
    }
}
