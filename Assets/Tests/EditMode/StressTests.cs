using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class StressTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = ScriptableObject.CreateInstance<SimulationRules>();

    [TearDown]
    public void TearDown()
    {
        SimulationRules.Bind(null);
        Object.DestroyImmediate(_rules);
    }

    private void Bind(bool sources)
    {
        var so = new SerializedObject(_rules);
        so.FindProperty("stressSourcesEnabled").boolValue = sources;
        so.ApplyModifiedPropertiesWithoutUndo();
        SimulationRules.Bind(_rules);
    }

    private static float Simulate(float energy, float environmentMul, bool exposed, bool hasKin, float seconds)
    {
        ref readonly var r = ref SimulationRules.Frame;
        float raw = 0f, alone = 0f;
        const float dt = 1f;
        for (float t = 0f; t < seconds; t += dt)
        {
            alone = CreatureCore.StepAloneSeconds(alone, hasKin, dt);
            raw += (CreatureCore.HungerStressRate(energy, in r) + CreatureCore.ColdStressRate(environmentMul, exposed, in r)
                  + CreatureCore.LonelyStressRate(alone, in r)) * dt;
            raw = math.saturate(raw) * math.exp(-dt / r.StressTau);
        }
        return raw;
    }

    [Test]
    public void HungryExposedLoneAgentGainsStress()
    {
        Bind(true);
        float stress = Simulate(0.1f, 1.5f, true, false, 120f);
        Assert.Greater(stress, 0.1f);
    }

    [Test]
    public void StressStaysFlatWithTheFlagOff()
    {
        Bind(false);
        Assert.AreEqual(0f, Simulate(0.1f, 1.5f, true, false, 120f));
    }

    [Test]
    public void EachSourceNeedsItsOwnCondition()
    {
        Bind(true);
        ref readonly var r = ref SimulationRules.Frame;
        Assert.AreEqual(0f, CreatureCore.HungerStressRate(0.9f, in r));
        Assert.Greater(CreatureCore.HungerStressRate(0.1f, in r), 0f);
        Assert.AreEqual(0f, CreatureCore.ColdStressRate(1.5f, false, in r));
        Assert.AreEqual(0f, CreatureCore.ColdStressRate(1f, true, in r));
        Assert.Greater(CreatureCore.ColdStressRate(1.5f, true, in r), 0f);
        Assert.AreEqual(0f, CreatureCore.LonelyStressRate(r.LonelinessWindow * 0.5f, in r));
        Assert.Greater(CreatureCore.LonelyStressRate(r.LonelinessWindow * 2f, in r), 0f);
        Assert.AreEqual(0f, CreatureCore.StepAloneSeconds(50f, true, 1f));
        Assert.AreEqual(51f, CreatureCore.StepAloneSeconds(50f, false, 1f));
    }

    [Test]
    public void LevelDoesNotFlickerAroundTheThresholds()
    {
        Bind(true);
        ref readonly var r = ref SimulationRules.Frame;
        Assert.Less(r.StressHighExit, r.StressHighEnter);
        Assert.Less(r.StressStressedExit, r.StressStressedEnter);

        float mid = (r.StressHighEnter + r.StressHighExit) * 0.5f;
        float high = r.StressHighEnter + 0.02f;
        int level = CreatureCore.StressCalm;
        int changes = 0;
        for (int i = 0; i < 200; i++)
        {
            int next = CreatureCore.NextStressLevel(level, i % 2 == 0 ? high : mid, in r);
            if (next != level) changes++;
            level = next;
        }
        Assert.AreEqual(CreatureCore.StressHigh, level);
        Assert.LessOrEqual(changes, 2);

        float low = (r.StressStressedEnter + r.StressStressedExit) * 0.5f;
        level = CreatureCore.StressCalm;
        changes = 0;
        float over = r.StressStressedEnter + 0.01f;
        for (int i = 0; i < 200; i++)
        {
            int next = CreatureCore.NextStressLevel(level, i % 2 == 0 ? over : low, in r);
            if (next != level) changes++;
            level = next;
        }
        Assert.AreEqual(CreatureCore.StressStressed, level);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void LevelFallsOnlyBelowTheExitThreshold()
    {
        Bind(true);
        ref readonly var r = ref SimulationRules.Frame;
        Assert.AreEqual(CreatureCore.StressHigh, CreatureCore.NextStressLevel(CreatureCore.StressHigh, r.StressHighExit, in r));
        Assert.AreEqual(CreatureCore.StressStressed, CreatureCore.NextStressLevel(CreatureCore.StressHigh, r.StressHighExit - 0.01f, in r));
        Assert.AreEqual(CreatureCore.StressCalm, CreatureCore.NextStressLevel(CreatureCore.StressStressed, r.StressStressedExit - 0.01f, in r));
        Assert.AreEqual(CreatureCore.StressCalm, CreatureCore.NextStressLevel(CreatureCore.StressCalm, r.StressStressedEnter - 0.01f, in r));
    }

    [Test]
    public void SmoothingFollowsTheRawValueSlowly()
    {
        Bind(true);
        ref readonly var r = ref SimulationRules.Frame;
        float smooth = 0f;
        smooth = CreatureCore.SmoothStress(smooth, 1f, 1f, in r);
        Assert.Greater(smooth, 0f);
        Assert.Less(smooth, 0.5f);
        for (int i = 0; i < 200; i++) smooth = CreatureCore.SmoothStress(smooth, 1f, 1f, in r);
        Assert.AreEqual(1f, smooth, 1e-3f);
    }

    [Test]
    public void ConsequencesAreNeutralAtZeroStressAndWithTheFlagOff()
    {
        Bind(true);
        ref readonly var on = ref SimulationRules.Frame;
        AssertNeutral(0f, in on);
        Assert.AreEqual(0f, CreatureCore.StressEffect(0.9f, CreatureCore.StressCalm, in on));

        Bind(false);
        ref readonly var off = ref SimulationRules.Frame;
        float effect = CreatureCore.StressEffect(0.9f, CreatureCore.StressHigh, in off);
        Assert.AreEqual(0f, effect);
        AssertNeutral(effect, in off);
    }

    private static void AssertNeutral(float effect, in SimulationRules.RulesFrame r)
    {
        Assert.AreEqual(1f, CreatureCore.StressDrainMul(effect, in r));
        Assert.AreEqual(1f, CreatureCore.StressRadiusMul(effect, in r));
        Assert.AreEqual(1f, CreatureCore.StressDiscountPower(effect, in r));
        Assert.AreEqual(1f, CreatureCore.StressLearningScale(effect, in r));
        Assert.AreEqual(0f, CreatureCore.StressAbandonChance(effect, in r));
    }

    [Test]
    public void ConsequencesFollowTheirFormulasAtFullStress()
    {
        Bind(true);
        ref readonly var r = ref SimulationRules.Frame;
        float effect = CreatureCore.StressEffect(1f, CreatureCore.StressHigh, in r);
        Assert.AreEqual(1f, effect);
        Assert.AreEqual(1f + r.StressDrainMul, CreatureCore.StressDrainMul(effect, in r), 1e-6f);
        Assert.AreEqual(1f - r.StressRadiusShrink, CreatureCore.StressRadiusMul(effect, in r), 1e-6f);
        Assert.AreEqual(1f + r.StressDiscountMul, CreatureCore.StressDiscountPower(effect, in r), 1e-6f);
        Assert.AreEqual(math.max(r.StressLrFloor, 1f - r.StressLrMul), CreatureCore.StressLearningScale(effect, in r), 1e-6f);
        Assert.AreEqual(r.StressAbandonChance, CreatureCore.StressAbandonChance(effect, in r), 1e-6f);
        Assert.GreaterOrEqual(CreatureCore.StressLearningScale(effect, in r), r.StressLrFloor);
    }

    [Test]
    public void BrainContextDiscountAndLearningScalesAreNeutralByDefault()
    {
        Bind(false);
        var brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, 32);
        try
        {
            var ctx = brain.CreateContext();
            Assert.AreEqual(0.9f, ctx.Gamma(0.9f));
            Assert.AreEqual(1f, ctx.CoordMLP.LearningScale);

            ctx.SetStressScales(0.5f, 2f);
            Assert.AreEqual(0.81f, ctx.Gamma(0.9f), 1e-6f);
            Assert.AreEqual(0.5f, ctx.CoordMLP.LearningScale);
            Assert.AreEqual(0.5f, ctx.ExecMLPs[0].LearningScale);
            ctx.Dispose();
        }
        finally
        {
            brain.Dispose();
            ContextSlabs.DisposeAll();
        }
    }

    [Test]
    public void StressMathDoesNotAllocate()
    {
        Bind(true);
        float sink = 0f;
        int level = 0;
        TestDelegate step = () =>
        {
            ref readonly var r = ref SimulationRules.Frame;
            sink += CreatureCore.HungerStressRate(0.2f, in r) + CreatureCore.ColdStressRate(1.4f, true, in r)
                  + CreatureCore.LonelyStressRate(100f, in r);
            sink = CreatureCore.SmoothStress(sink, 0.7f, 0.5f, in r);
            level = CreatureCore.NextStressLevel(level, sink, in r);
            float effect = CreatureCore.StressEffect(sink, level, in r);
            sink += CreatureCore.StressDrainMul(effect, in r) + CreatureCore.StressRadiusMul(effect, in r)
                  + CreatureCore.StressDiscountPower(effect, in r) + CreatureCore.StressLearningScale(effect, in r);
        };
        AllocationAssert.Zero(step, 200, "CreatureCore stress math");
    }

    [Test]
    public void StressAccountingSumsToTheTotalAdded()
    {
        var stats = new ColonyStats();
        stats.RecordStress(ColonyStats.StressSource.Hunger, 0.25f);
        stats.RecordStress(ColonyStats.StressSource.Cold, 0.125f);
        stats.RecordStress(ColonyStats.StressSource.Lonely, 0.0625f);
        stats.RecordStress(ColonyStats.StressSource.Task, 0.05f);
        stats.RecordStress(ColonyStats.StressSource.Threat, 0.5f);
        stats.RecordStressTime(4f, false);
        stats.RecordStressTime(1f, true);

        double sum = 0d;
        for (int i = 0; i < stats.StressAdded.Length; i++) sum += stats.StressAdded[i];
        Assert.AreEqual(0.9875d, sum, 1e-6d);
        Assert.AreEqual(5d, stats.StressSeconds);
        Assert.AreEqual(1d, stats.StressHighSeconds);
    }
}
