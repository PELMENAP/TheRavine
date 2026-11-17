using NUnit.Framework;
using UnityEngine;

public class NestSiteScoreTests
{
    private SimulationRules _rules;
    private SimulationRules.RulesFrame _frame;

    [SetUp]
    public void SetUp()
    {
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        _frame = new SimulationRules.RulesFrame(_rules);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_rules);

    private static TerrainSample Terrain(float slope, float relativeHeight)
        => new TerrainSample(0.5f, slope, 0f, 0f, 0f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
            0.25f, 0.25f, 0.25f, 0.25f, 0.5f, 0.5f, 0.5f, relativeHeight);

    [Test]
    public void FlatGroundWithFoodScoresOne()
    {
        var t = Terrain(0f, 0f);
        Assert.AreEqual(1f, ColonyState.SiteScore(in _frame, in t, _frame.QuorumFoodNorm, _frame.QuorumMinDistance + 1f), 1e-5f);
    }

    [Test]
    public void SlopeAboveLimitAddsLessThanFlatGroundWithFood()
    {
        var flat  = Terrain(0f, 0f);
        var steep = Terrain(_frame.QuorumSlopeLimit * 1.5f, 0f);
        float far = _frame.QuorumMinDistance + 1f;
        float good = ColonyState.SiteScore(in _frame, in flat, _frame.QuorumFoodNorm, far);
        float bad  = ColonyState.SiteScore(in _frame, in steep, _frame.QuorumFoodNorm, far);
        Assert.Less(bad, good);
    }

    [Test]
    public void DepressionBottomAddsLessThanFlatGroundWithFood()
    {
        var flat = Terrain(0f, 0f);
        var pit  = Terrain(0f, -1f);
        float far = _frame.QuorumMinDistance + 1f;
        float good = ColonyState.SiteScore(in _frame, in flat, _frame.QuorumFoodNorm, far);
        float bad  = ColonyState.SiteScore(in _frame, in pit, _frame.QuorumFoodNorm, far);
        Assert.Less(bad, good);
    }

    [Test]
    public void BareGroundScoresLessThanGroundWithFood()
    {
        var flat = Terrain(0f, 0f);
        float far = _frame.QuorumMinDistance + 1f;
        Assert.Less(ColonyState.SiteScore(in _frame, in flat, 0f, far), ColonyState.SiteScore(in _frame, in flat, _frame.QuorumFoodNorm, far));
    }

    [Test]
    public void SiteInsideMinimumDistanceScoresZero()
    {
        var flat = Terrain(0f, 0f);
        Assert.AreEqual(0f, ColonyState.SiteScore(in _frame, in flat, _frame.QuorumFoodNorm, _frame.QuorumMinDistance * 0.5f));
    }

    [Test]
    public void ScoreStaysInUnitRange()
    {
        for (float slope = 0f; slope <= 1f; slope += 0.25f)
        for (float rel = -1f; rel <= 1f; rel += 0.5f)
        for (float food = 0f; food <= 2f; food += 0.5f)
        {
            var t = Terrain(slope, rel);
            float s = ColonyState.SiteScore(in _frame, in t, food, 1000f);
            Assert.GreaterOrEqual(s, 0f);
            Assert.LessOrEqual(s, 1f);
        }
    }

    [Test]
    public void SiteScoreAllocatesNothing()
    {
        var t = Terrain(0.2f, -0.1f);
        float sink = 0f;
        AllocationAssert.Zero(() => sink += ColonyState.SiteScore(in _frame, in t, 0.1f, 50f), 256, "ColonyState.SiteScore");
        Assert.GreaterOrEqual(sink, 0f);
    }
}
