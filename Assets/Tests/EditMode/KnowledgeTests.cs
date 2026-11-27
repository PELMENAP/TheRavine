using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class KnowledgeTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = TestRules.Bind();

    [TearDown]
    public void TearDown() => TestRules.Release(_rules);

    [Test]
    public void EvaluatesPostfixAndRejectsMalformed()
    {
        var ops  = new byte[] { KnowledgeMath.OpVar, KnowledgeMath.OpConst, KnowledgeMath.OpMul, KnowledgeMath.OpConst, KnowledgeMath.OpAdd };
        var args = new float[] { 1f, 2f, 0f, -1f, 0f };
        var x    = new float[] { 0f, 3f, 0f, 0f, 0f, 0f };
        Assert.IsTrue(KnowledgeMath.IsValid(ops, args, 5, KnowledgeSystem.Features));
        Assert.AreEqual(5f, KnowledgeMath.Evaluate(ops, args, 5, x), 1e-6f);
        Assert.IsFalse(KnowledgeMath.IsValid(ops, args, 4, KnowledgeSystem.Features));
        Assert.IsFalse(KnowledgeMath.IsValid(new byte[] { KnowledgeMath.OpAdd }, new float[1], 1, KnowledgeSystem.Features));
        Assert.IsFalse(KnowledgeMath.IsValid(new byte[] { KnowledgeMath.OpVar }, new float[] { 9f }, 1, KnowledgeSystem.Features));
        Assert.IsFalse(KnowledgeMath.IsValid(new byte[] { KnowledgeMath.OpConst }, new float[] { float.NaN }, 1, KnowledgeSystem.Features));
    }

    [Test]
    public void MutationKeepsExpressionsWellFormed()
    {
        var ops  = new byte[KnowledgeMath.MaxNodes];
        var args = new float[KnowledgeMath.MaxNodes];
        int count = 0;
        for (int i = 0; i < 5000; i++)
        {
            count = KnowledgeMath.Mutate(ops, args, count, KnowledgeSystem.Features, 0.5f);
            Assert.IsTrue(KnowledgeMath.IsValid(ops, args, count, KnowledgeSystem.Features), $"step {i}");
            Assert.LessOrEqual(count, KnowledgeMath.MaxNodes);
        }
    }

    private static float Run(KnowledgeSystem system, int index, System.Func<float[], float> target, System.Random rng, int rounds)
    {
        ref readonly var r = ref SimulationRules.Frame;
        var x = new float[KnowledgeSystem.Features];
        float reward = 0f, now = 0f;
        for (int t = 0; t < rounds; t++)
        {
            for (int k = 0; k < x.Length; k++) x[k] = (float)rng.NextDouble();
            system.Push(index, x, target(x));
            for (int s = 0; s < 20; s++)
            {
                now += r.KnowledgeSearchInterval;
                reward += system.Search(index, now, in r);
            }
        }
        return reward;
    }

    private static float HoldError(KnowledgeSystem system, int index, System.Func<float[], float> target, System.Random rng)
    {
        var x = new float[KnowledgeSystem.Features];
        double sum = 0d;
        for (int i = 0; i < 200; i++)
        {
            for (int k = 0; k < x.Length; k++) x[k] = (float)rng.NextDouble();
            float p = system.Predict(index, x);
            float d = (float.IsNaN(p) ? 0f : p) - target(x);
            sum += d * d;
        }
        return (float)(sum / 200);
    }

    [Test]
    public void LearnsHiddenRuleBetterThanMeanOnNewData()
    {
        var system = new KnowledgeSystem(1);
        var rng = new System.Random(3);
        System.Func<float[], float> rule = x => 2f * x[4] - 1f;
        Run(system, 0, rule, rng, 400);
        Assert.Greater(system.BestNodes(0), 1);
        float learned = HoldError(system, 0, rule, new System.Random(99));
        float baseline = 4f / 12f;
        Assert.Less(learned, baseline * 0.5f, $"learned {learned}");
    }

    [Test]
    public void PureNoiseEarnsLessThanStructure()
    {
        var noiseRng = new System.Random(5);
        var noise = new KnowledgeSystem(1);
        float noiseReward = Run(noise, 0, _ => (float)noiseRng.NextDouble() * 2f - 1f, new System.Random(6), 400);
        var structure = new KnowledgeSystem(1);
        float structureReward = Run(structure, 0, x => 2f * x[4] - 1f, new System.Random(6), 400);
        Assert.Greater(structureReward, noiseReward * 2f + 1e-4f, $"noise {noiseReward} structure {structureReward}");
    }

    [Test]
    public void RepeatedSearchOnSameDataStopsRewarding()
    {
        ref readonly var r = ref SimulationRules.Frame;
        var system = new KnowledgeSystem(1);
        var rng = new System.Random(8);
        var x = new float[KnowledgeSystem.Features];
        for (int t = 0; t < KnowledgeSystem.Observations; t++)
        {
            for (int k = 0; k < x.Length; k++) x[k] = (float)rng.NextDouble();
            system.Push(0, x, x[2] - x[3]);
        }
        float now = 0f;
        for (int s = 0; s < 4000; s++) { now += r.KnowledgeSearchInterval; system.Search(0, now, in r); }
        float late = 0f;
        for (int s = 0; s < 200; s++) { now += r.KnowledgeSearchInterval; late += system.Search(0, now, in r); }
        Assert.Less(late, r.KnowledgeRewardCap);
    }

    [Test]
    public void TeachingNeedsAModelAndIsRevalidated()
    {
        ref readonly var r = ref SimulationRules.Frame;
        var system = new KnowledgeSystem(2);
        Assert.IsFalse(system.Teach(0, 1, in r));
        Run(system, 0, x => 2f * x[4] - 1f, new System.Random(4), 300);
        Assert.IsTrue(system.Teach(0, 1, in r));
        Assert.IsTrue(system.HasTaught(1));
        Assert.AreEqual(0, system.BestNodes(1));
        Run(system, 1, x => 2f * x[4] - 1f, new System.Random(12), 20);
        Assert.IsFalse(system.HasTaught(1));
        Assert.Greater(system.BestNodes(1), 0);
    }

    [Test]
    public void SearchAllocatesNothing()
    {
        ref readonly var r = ref SimulationRules.Frame;
        var system = new KnowledgeSystem(1);
        var rng = new System.Random(2);
        var x = new float[KnowledgeSystem.Features];
        for (int t = 0; t < KnowledgeSystem.Observations; t++)
        {
            for (int k = 0; k < x.Length; k++) x[k] = (float)rng.NextDouble();
            system.Push(0, x, x[1]);
        }
        float now = 0f, interval = r.KnowledgeSearchInterval, sink = 0f;
        var rules = SimulationRules.Frame;
        AllocationAssert.Zero(() => { now += interval; sink += system.Search(0, now, in rules); }, 300, "KnowledgeSystem.Search");
    }
}
