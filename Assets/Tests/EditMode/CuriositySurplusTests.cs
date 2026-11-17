using NUnit.Framework;
using UnityEngine;

public class CuriositySurplusTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = ScriptableObject.CreateInstance<SimulationRules>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_rules);

    [TestCase(0f)]
    [TestCase(0.3f)]
    [TestCase(0.6f)]
    public void AtOrBelowFloorSurplusIsZeroAndRewardIsUnchanged(float energy)
    {
        float floor = _rules.CuriosityEnergyFloor;
        float surplus = SharedHierarchicalBrain.EnergySurplus(energy, floor);
        Assert.AreEqual(0f, surplus);
        Assert.AreEqual(1f, 1f + _rules.CuriosityEnergyGain * surplus);
        Assert.AreEqual(0f, SharedHierarchicalBrain.RarityTerm(_rules.CuriosityRarityWeight, surplus, 0f));
    }

    [Test]
    public void FullEnergyGivesFullSurplus()
    {
        Assert.AreEqual(1f, SharedHierarchicalBrain.EnergySurplus(1f, _rules.CuriosityEnergyFloor), 1e-6f);
        Assert.AreEqual(1f + _rules.CuriosityEnergyGain, 1f + _rules.CuriosityEnergyGain * 1f, 1e-6f);
    }

    [Test]
    public void RarePlanGetsMoreThanCommonPlan()
    {
        float w = _rules.CuriosityRarityWeight;
        float rare   = SharedHierarchicalBrain.RarityTerm(w, 1f, 0.01f);
        float common = SharedHierarchicalBrain.RarityTerm(w, 1f, 0.8f);
        Assert.Greater(rare, common);
        Assert.Greater(common, 0f);
    }

    [Test]
    public void SurplusAndRarityAllocateNothing()
    {
        float sink = 0f;
        AllocationAssert.Zero(() =>
        {
            float s = SharedHierarchicalBrain.EnergySurplus(0.8f, 0.6f);
            sink += SharedHierarchicalBrain.RarityTerm(0.1f, s, 0.2f);
        }, 256, "curiosity surplus");
        Assert.Greater(sink, 0f);
    }
}
