using NUnit.Framework;
using UnityEngine;

public class FamiliarityTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = ScriptableObject.CreateInstance<SimulationRules>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_rules);

    [Test]
    public void GrowsTowardOneWithTimeConstant()
    {
        float tau = _rules.FamiliarityTau;
        float f = 0f;
        for (int i = 0; i < 20; i++) f = FamiliarityMath.Grow(f, tau / 20f, tau);
        Assert.AreEqual(1f - Mathf.Exp(-1f), f, 1e-4f);
    }

    [Test]
    public void CalmIsReachedAfterAboutOneTimeConstant()
    {
        float tau = _rules.FamiliarityTau;
        float f = 0f;
        float t = 0f;
        while (f <= _rules.FamiliarityCalm && t < tau * 10f)
        {
            f = FamiliarityMath.Grow(f, 0.5f, tau);
            t += 0.5f;
        }
        Assert.Greater(t, tau * 0.5f);
        Assert.Less(t, tau * 2f);
    }

    [Test]
    public void NeverLeavesUnitRange()
    {
        float f = 0.99f;
        for (int i = 0; i < 100; i++) f = FamiliarityMath.Grow(f, 5f, 1f);
        Assert.LessOrEqual(f, 1f);
        Assert.GreaterOrEqual(FamiliarityMath.Grow(0f, -1f, 1f), 0f);
    }

    [Test]
    public void GiftJumpsAndSaturates()
    {
        Assert.AreEqual(_rules.FamiliarityGift, FamiliarityMath.Gift(0f, _rules.FamiliarityGift), 1e-6f);
        Assert.AreEqual(1f, FamiliarityMath.Gift(0.9f, 0.5f), 1e-6f);
    }

    [Test]
    public void UnboundModelIgnoresGiftAndHarm()
    {
        var model = new EntityModel();
        model.NoteGift(null);
        model.BreakFamiliarity(null);
        Assert.AreEqual(0f, model.Familiarity);
    }

    [Test]
    public void GrowthAllocatesNothing()
    {
        float f = 0f;
        AllocationAssert.Zero(() => f = FamiliarityMath.Grow(f, 0.1f, 20f), 256, "FamiliarityMath.Grow");
    }
}
