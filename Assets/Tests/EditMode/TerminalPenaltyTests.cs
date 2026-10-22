using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class TerminalPenaltyTests
{
    private static readonly float[] Scales        = { -2f, -0.5f, 0f, 0.25f, 1f, 2f, 10f };
    private static readonly float[] Sensitivities = { -1f, 0f, 0.5f, 2f };
    private static readonly float[] Gammas        = { 0.5f, 0.9f, 0.99f, 1f };

    private static DeathCause[] Causes => (DeathCause[])Enum.GetValues(typeof(DeathCause));

    private static void AssertNonPositive(SimulationRules rules, string source)
    {
        foreach (var cause in Causes)
        {
            Assert.GreaterOrEqual(rules.DeathPenaltyMul(cause), 0f, $"{source}: DeathPenaltyMul({cause}) < 0 инвертирует штраф");

            for (int i = 0; i <= 24; i++)
            {
                float z = -3f + i * 0.25f;
                Assert.LessOrEqual(TerminalPenaltyMath.For(rules, cause, true, z), 0f, $"{source}: {cause}, z={z}");
                Assert.LessOrEqual(TerminalPenaltyMath.For(rules, cause, false, z), 0f, $"{source}: {cause}, z={z}, без статистики");
            }
        }
    }

    [Test]
    public void DefaultRulesPenaltyIsNonPositiveForEveryCause()
    {
        var rules = ScriptableObject.CreateInstance<SimulationRules>();
        try
        {
            Assert.AreEqual(-1f, rules.TerminalPenalty);
            AssertNonPositive(rules, "default");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void ProjectRulesPenaltyIsNonPositiveForEveryCause()
    {
        string[] guids = AssetDatabase.FindAssets("t:SimulationRules");
        Assert.IsNotEmpty(guids);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var rules = AssetDatabase.LoadAssetAtPath<SimulationRules>(path);
            Assert.LessOrEqual(rules.TerminalPenalty, 0f, path);
            AssertNonPositive(rules, path);
        }
    }

    [Test]
    public void ScaleIsNonNegative()
    {
        foreach (float min in Scales)
        foreach (float max in Scales)
        foreach (float sensitivity in Sensitivities)
            for (int i = 0; i <= 24; i++)
            {
                float z = -3f + i * 0.25f;
                float scale = TerminalPenaltyMath.Scale(z, sensitivity, min, max);
                Assert.GreaterOrEqual(scale, 0f, $"min={min}, max={max}, s={sensitivity}, z={z}");
                Assert.GreaterOrEqual(scale, Math.Max(min, 0f));
            }
    }

    [Test]
    public void ComputeWithNegativePenaltyIsNonPositive()
    {
        foreach (float min in Scales)
        foreach (float max in Scales)
        foreach (float sensitivity in Sensitivities)
            for (int i = 0; i <= 24; i++)
            {
                float z = -3f + i * 0.25f;
                Assert.LessOrEqual(TerminalPenaltyMath.Compute(-1f, true, z, sensitivity, min, max), 0f);
            }
    }

    [Test]
    public void RewardNormalizationKeepsPenaltySign()
    {
        var fresh = new RunningMeanStd();
        Assert.Less(fresh.Scale(-1f, 5f, 0.05f), 0f);

        var norm = new RunningMeanStd();
        for (int i = 0; i < 100; i++) norm.Update(i % 7 - 2f);
        Assert.Greater(norm.Mean, 0f);
        Assert.Less(norm.Scale(-1f, 5f, 0.05f), 0f);
        Assert.Less(norm.Scale(-0.001f, 5f, 0.05f), 0f);
    }

    [Test]
    public void FlushTerminalRewardIsNegativeForNegativePenalty()
    {
        var ring = new DecisionRing(8, 4, 3);
        for (int i = 0; i < 6; i++)
        {
            var item = ring.Push(i + 1);
            item.StartTime  = i * 0.5f;
            item.Evaluation = 0f;
        }

        const float deathTime = 2.5f;
        foreach (float gamma in Gammas)
            for (int i = 0; i < ring.Count; i++)
            {
                var item = ring[i];
                float elapsed = deathTime - item.StartTime;
                float discount = DelayedPerceptron.DiscountTime(gamma, elapsed);
                Assert.Greater(discount, 0f);
                Assert.LessOrEqual(discount, 1f);
                Assert.Less(DelayedPerceptron.TerminalReward(item.Evaluation, -1f, gamma, elapsed), 0f,
                    $"gamma={gamma}, slot={i}");
            }
    }

    [Test]
    public void LearningRateIsAlwaysPositive()
    {
        float[] times  = { 0f, 1f, 1e3f, 1e6f, 1e9f };
        float[] bases  = { -1f, 0f, 0.01f };
        float[] decays = { -1f, 0f, 5e-4f, 1f, 1e3f };
        float[] mins   = { -1f, 0f, 1e-9f, 1e-3f };

        foreach (float t in times)
        foreach (float b in bases)
        foreach (float d in decays)
        foreach (float m in mins)
        {
            float lr = SharedHierarchicalBrain.ResolveLearningRate(t, b, d, m);
            Assert.Greater(lr, 0f, $"t={t}, base={b}, decay={d}, min={m}");
            Assert.IsTrue(float.IsFinite(lr), $"t={t}, base={b}, decay={d}, min={m}");
        }

        Assert.AreEqual(0.01f, SharedHierarchicalBrain.ResolveLearningRate(0f, 0.01f, 5e-4f, 1e-3f));
    }

    [Test]
    public void ProjectRulesLearningRateIsValid()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:SimulationRules"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var rules = AssetDatabase.LoadAssetAtPath<SimulationRules>(path);
            Assert.Greater(rules.MinLearningRate, 0f, path);
            Assert.Greater(rules.OptimizerBaseLearningRate, 0f, path);
            Assert.GreaterOrEqual(rules.LrDecayPerSecond, 0f, path);
        }
    }
}
