using System;
using NUnit.Framework;
using Unity.Mathematics;

public class CodexMathTests
{
    private const int K = (int)VarianceAxis.Count;
    private const float Inertia = 6f;

    private static LawRecord Law(int id, float support, params (VarianceAxis axis, float value)[] entries)
    {
        var law = new LawRecord { Id = id, Author = 1, Support = support, Alive = 1 };
        var delta = LawRecord.Delta(ref law);
        for (int i = 0; i < entries.Length; i++) delta[(int)entries[i].axis] = entries[i].value;
        return law;
    }

    private static float[] Filled(float value)
    {
        var array = new float[K];
        for (int i = 0; i < K; i++) array[i] = value;
        return array;
    }

    private static float[] Aggregate(LawRecord[] laws, float[] opposition = null, float inertia = Inertia, float low = -0.5f, float high = 0.5f)
    {
        var theta = new float[K];
        CodexMath.Aggregate(laws, opposition ?? new float[K], inertia, Filled(low), Filled(high), theta);
        return theta;
    }

    [Test]
    public void ZeroSupportGivesZeroTheta()
    {
        var theta = Aggregate(new[] { Law(1, 0f, (VarianceAxis.Metabolism, 0.3f), (VarianceAxis.FoodGrowth, -0.2f)) });
        for (int k = 0; k < K; k++) Assert.AreEqual(0f, theta[k]);
    }

    [Test]
    public void NoLawsGivesZeroTheta()
    {
        var theta = Aggregate(Array.Empty<LawRecord>());
        for (int k = 0; k < K; k++) Assert.AreEqual(0f, theta[k]);
    }

    [Test]
    public void DeadLawIsIgnored()
    {
        var law = Law(1, 50f, (VarianceAxis.Metabolism, 0.3f));
        law.Alive = 0;
        Assert.AreEqual(0f, Aggregate(new[] { law })[(int)VarianceAxis.Metabolism]);
    }

    [Test]
    public void OppositeLawsWithEqualSupportCancel()
    {
        var theta = Aggregate(new[]
        {
            Law(1, 12f, (VarianceAxis.FoodValue, 0.25f)),
            Law(2, 12f, (VarianceAxis.FoodValue, -0.25f)),
        });
        Assert.AreEqual(0f, theta[(int)VarianceAxis.FoodValue], 1e-6f);
    }

    [Test]
    public void LogSpaceMakesPlusAndMinusCancelInMultiplier()
    {
        var theta = new[] { 0.3f, -0.3f };
        var multipliers = new float[2];
        CodexMath.Multipliers(theta, multipliers);
        Assert.AreEqual(1f, multipliers[0] * multipliers[1], 1e-6f);
    }

    [Test]
    public void ThetaNeverLeavesBounds()
    {
        var laws = new[]
        {
            Law(1, 1e6f, (VarianceAxis.Metabolism, 0.9f), (VarianceAxis.FoodGrowth, -0.9f)),
            Law(2, 1e6f, (VarianceAxis.Metabolism, 0.9f)),
        };
        var theta = Aggregate(laws, null, Inertia, -0.4f, 0.2f);
        Assert.AreEqual(0.2f, theta[(int)VarianceAxis.Metabolism], 1e-6f);
        Assert.AreEqual(-0.4f, theta[(int)VarianceAxis.FoodGrowth], 1e-6f);
        for (int k = 0; k < K; k++) Assert.That(theta[k], Is.InRange(-0.4f, 0.2f));
    }

    [Test]
    public void OppositionPullsThetaTowardZero()
    {
        var laws = new[] { Law(1, 10f, (VarianceAxis.FoodGrowth, 0.3f)) };
        var free = Aggregate(laws)[(int)VarianceAxis.FoodGrowth];
        var opposition = new float[K];
        opposition[(int)VarianceAxis.FoodGrowth] = 30f;
        var held = Aggregate(laws, opposition)[(int)VarianceAxis.FoodGrowth];

        Assert.Greater(free, held);
        Assert.Greater(held, 0f);
        Assert.AreEqual(10f * 0.3f / (Inertia + 30f + 10f), held, 1e-6f);
    }

    [Test]
    public void SingleSmallInvestmentIsBoundedByInertia()
    {
        float support = CodexMath.VoteGain(1f, 1f);
        var theta = Aggregate(new[] { Law(1, support, (VarianceAxis.Aggression, 0.3f)) });
        Assert.LessOrEqual(theta[(int)VarianceAxis.Aggression], 0.3f / (1f + Inertia) + 1e-6f);
        Assert.Less(theta[(int)VarianceAxis.Aggression], 0.3f * 0.2f);
    }

    [Test]
    public void AggregateIsExactWeightedAverageWithInertia()
    {
        var theta = Aggregate(new[]
        {
            Law(1, 4f, (VarianceAxis.Pheromone, 0.2f)),
            Law(2, 2f, (VarianceAxis.Pheromone, -0.1f)),
        });
        Assert.AreEqual((4f * 0.2f - 2f * 0.1f) / (Inertia + 6f), theta[(int)VarianceAxis.Pheromone], 1e-6f);
    }

    [Test]
    public void QuadraticVotingQuadruplesEnergyDoublesGain()
    {
        Assert.AreEqual(2f * CodexMath.VoteGain(9f, 1.5f), CodexMath.VoteGain(36f, 1.5f), 1e-5f);
        Assert.AreEqual(0f, CodexMath.VoteGain(0f, 1f));
        Assert.AreEqual(0f, CodexMath.VoteGain(-5f, 1f));
    }

    [Test]
    public void CostIsPriceDotProduct()
    {
        var price = new float[K];
        price[(int)VarianceAxis.FoodGrowth] = 0.4f;
        price[(int)VarianceAxis.Metabolism] = -0.5f;

        var helps = new float[K];
        helps[(int)VarianceAxis.FoodGrowth] = 0.2f;
        Assert.AreEqual(0.08f, CodexMath.Cost(helps, price), 1e-6f);

        var hurts = new float[K];
        hurts[(int)VarianceAxis.Metabolism] = 0.2f;
        Assert.Less(CodexMath.Cost(hurts, price), 0f);

        var balanced = new float[K];
        balanced[(int)VarianceAxis.FoodGrowth] = 0.25f;
        balanced[(int)VarianceAxis.Metabolism] = 0.2f;
        Assert.AreEqual(0f, CodexMath.Cost(balanced, price), 1e-6f);
    }

    [Test]
    public void DecayHalvesSupportAfterLnTwoOverRate()
    {
        float rate = 0.004f;
        float half = math.log(2f) / rate;
        Assert.AreEqual(5f, CodexMath.Decay(10f, rate, half), 1e-4f);
        Assert.AreEqual(10f, CodexMath.Decay(10f, rate, 0f), 1e-6f);
        Assert.AreEqual(10f, CodexMath.Decay(10f, rate, -3f), 1e-6f);
    }

    [Test]
    public void AggregateDoesNotAllocate()
    {
        var laws = new[] { Law(1, 10f, (VarianceAxis.Metabolism, 0.2f)), Law(2, 5f, (VarianceAxis.FoodGrowth, -0.1f)) };
        var opposition = new float[K];
        var low = Filled(-0.5f);
        var high = Filled(0.5f);
        var theta = new float[K];
        AllocationAssert.Zero(() => CodexMath.Aggregate(laws, opposition, Inertia, low, high, theta), 50, "CodexMath.Aggregate");
    }

    [Test]
    public void EstimatorKeepsPriorUntilMinSamples()
    {
        var prior = new[] { 0.5f, -0.25f, 0f };
        var estimator = new CodexPriceEstimator(3, 0.98f, 10, 1e-6f, 10f, prior);
        var rng = new System.Random(3);
        for (int i = 0; i < 5; i++) estimator.Add(new[] { (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() }, (float)rng.NextDouble());

        Assert.IsTrue(estimator.UsesPrior);
        for (int k = 0; k < 3; k++) Assert.AreEqual(prior[k], estimator.Prices[k]);
    }

    [Test]
    public void EstimatorConvergesOnNoisyLinearData()
    {
        var truth = new[] { 0.6f, -0.4f, 0.15f, 0f };
        var estimator = new CodexPriceEstimator(4, 0.99f, 20, 1e-6f, 10f, new float[4]);
        var rng = new System.Random(11);
        var theta = new float[4];

        for (int i = 0; i < 600; i++)
        {
            float flux = 0.3f;
            for (int k = 0; k < 4; k++)
            {
                theta[k] = (float)(rng.NextDouble() - 0.5);
                flux += truth[k] * theta[k];
            }
            flux += (float)(rng.NextDouble() - 0.5) * 0.02f;
            Assert.IsTrue(estimator.Add(theta, flux));
        }

        Assert.IsFalse(estimator.UsesPrior);
        for (int k = 0; k < 4; k++) Assert.AreEqual(truth[k], estimator.Prices[k], 0.05f, $"axis {k}");
    }

    [Test]
    public void EstimatorRejectsNonFiniteSamples()
    {
        var estimator = new CodexPriceEstimator(3, 0.98f, 4, 1e-6f, 10f, new float[3]);
        Assert.IsTrue(estimator.Add(new[] { 0.1f, 0.2f, 0.3f }, 1f));

        Assert.IsFalse(estimator.Add(new[] { float.NaN, 0f, 0f }, 1f));
        Assert.IsFalse(estimator.Add(new[] { 0f, float.PositiveInfinity, 0f }, 1f));
        Assert.IsFalse(estimator.Add(new[] { 0f, 0f, 0f }, float.NaN));
        Assert.IsFalse(estimator.Add(new[] { 0f, 0f, 0f }, float.NegativeInfinity));
        Assert.IsFalse(estimator.Add(new[] { 0f, 0f }, 1f));

        Assert.AreEqual(1, estimator.Samples);
        Assert.AreEqual(5, estimator.Rejected);
        for (int k = 0; k < 3; k++) Assert.IsTrue(math.isfinite(estimator.Prices[k]));
    }

    [Test]
    public void EstimatorStaysFiniteWithoutExcitation()
    {
        var estimator = new CodexPriceEstimator(3, 0.98f, 2, 1e-4f, 10f, new[] { 0.2f, 0f, 0f });
        for (int i = 0; i < 2000; i++) estimator.Add(new float[3], 0.1f);

        for (int k = 0; k < 3; k++) Assert.AreEqual(k == 0 ? 0.2f : 0f, estimator.Prices[k]);

        var theta = new float[3];
        var rng = new System.Random(5);
        for (int i = 0; i < 100; i++)
        {
            for (int k = 0; k < 3; k++) theta[k] = (float)rng.NextDouble();
            estimator.Add(theta, 0.1f + 0.5f * theta[0]);
        }
        for (int k = 0; k < 3; k++) Assert.IsTrue(math.isfinite(estimator.Prices[k]));
    }

    [Test]
    public void EstimatorStateRoundTrips()
    {
        var a = new CodexPriceEstimator(3, 0.98f, 3, 1e-6f, 10f, new float[3]);
        var rng = new System.Random(2);
        var theta = new float[3];
        for (int i = 0; i < 40; i++)
        {
            for (int k = 0; k < 3; k++) theta[k] = (float)rng.NextDouble();
            a.Add(theta, theta[0] - theta[1]);
        }

        var w = new float[3];
        var p = new float[9];
        var mean = new float[3];
        a.CopyState(w, p, mean, out float meanFlux, out int samples);

        var b = new CodexPriceEstimator(3, 0.98f, 3, 1e-6f, 10f, new float[3]);
        Assert.IsTrue(b.TryRestore(w, p, mean, meanFlux, samples));
        for (int k = 0; k < 3; k++) Assert.AreEqual(a.Prices[k], b.Prices[k]);

        w[1] = float.NaN;
        Assert.IsFalse(b.TryRestore(w, p, mean, meanFlux, samples));
    }

    [Test]
    public void EveryCodexErrorHasMessage()
    {
        for (int i = 0; i < (int)CodexError.Count; i++)
        {
            string text = CodexErrorText.Message((CodexError)i);
            Assert.IsFalse(string.IsNullOrWhiteSpace(text), ((CodexError)i).ToString());
            Assert.AreNotEqual(CodexErrorText.Message(CodexError.Count), text, ((CodexError)i).ToString());
        }
    }
}
