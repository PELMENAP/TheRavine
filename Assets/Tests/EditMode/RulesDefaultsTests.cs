using NUnit.Framework;
using UnityEngine;

public class RulesDefaultsTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = ScriptableObject.CreateInstance<SimulationRules>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_rules);

    [Test]
    public void MovedConstantsKeepPreviousValues()
    {
        var r = _rules;
        Assert.AreEqual(3f, r.StartupDelaySeconds);
        Assert.AreEqual(3f, r.TickLoopWarmupSeconds);
        Assert.AreEqual(1f, r.FitnessStatsInterval);
        Assert.AreEqual(1f, r.AgentTickInterval);

        Assert.AreEqual(3f, r.NestFarRadiusMul);
        Assert.AreEqual(2f, r.ChildSpawnOffset);
        Assert.AreEqual(5f, r.ChildSpawnLift);
        Assert.AreEqual(0.25f, r.FitnessSpreadMedianFraction);

        Assert.AreEqual(1f / 60f, r.CycleFallbackDt);
        Assert.AreEqual(0.1f, r.MinSpeedMul);
        Assert.AreEqual(0.1f, r.BlindMinDetectionFraction);
        Assert.AreEqual(0.1f, r.TimingMinSpeed);
        Assert.AreEqual(0.5f, r.DirectionMinDistance);
        Assert.AreEqual(2f, r.FlockDirectionRadiusMul);
        Assert.AreEqual(0.5f, r.FleeMinLegLength);
        Assert.AreEqual(2f, r.KinSearchRangeMul);
        Assert.AreEqual(0.05f, r.ThreatenMinHoldSeconds);
        Assert.AreEqual(2f, r.PlannerReplanAngleMul);

        Assert.AreEqual(1.5f, r.ExplorationEntropyTarget);
        Assert.AreEqual(0.05f, r.MinTrainAdvantage);
        Assert.AreEqual(0.05f, r.CoordExplorationEpsilon);
        Assert.AreEqual(0.15f, r.ExecExplorationEpsilon);
        Assert.AreEqual(0.01f, r.OptimizerBaseLearningRate);
        Assert.AreEqual(1e-5f, r.OptimizerWeightDecay);
        Assert.AreEqual(3f, r.OptimizerMaxGradNorm);

        Assert.AreEqual(0.85f, r.SplitIntegrityPenalty);
        Assert.AreEqual(0.25f, r.SuperinfectionBoost);
        Assert.AreEqual(8, r.TamedThreshold);
        Assert.AreEqual(12, r.LineageProximityBits);
        Assert.AreEqual(0.01f, r.SharpnessStepScale);
        Assert.AreEqual(0.05f, r.SharpnessMin);
        Assert.AreEqual(3f, r.SharpnessMax);
        Assert.AreEqual(0.08f, r.BaseMutationRate);
        Assert.AreEqual(0.55f, r.MutationPointWeight);
        Assert.AreEqual(0.15f, r.MutationInsertWeight);
        Assert.AreEqual(0.15f, r.MutationDeleteWeight);
        Assert.AreEqual(0.4f, r.MutationDoubleFlipChance);
        Assert.AreEqual(3f, r.MutationAmpGain);
        Assert.AreEqual(0.001f, r.MutationRateMin);
        Assert.AreEqual(0.5f, r.MutationRateMax);
        Assert.AreEqual(2f, r.MutationRateDeltaGain);
        Assert.AreEqual(4f, r.RibosomeMultiplierMax);
        Assert.AreEqual(0.1f, r.RibosomeMetabolismMin);
        Assert.AreEqual(2f, r.RibosomeBiasLimit);
        Assert.AreEqual(0.5f, r.RibosomeSharpnessDeltaLimit);
        Assert.AreEqual(0.5f, r.RibosomeMutationDeltaLimit);
        Assert.AreEqual(1.35f, r.InfectionAccuracyBias);
        Assert.AreEqual(0.35f, r.RecombinationChance);
        Assert.AreEqual(0.05f, r.InfectionSelectionFloor);
        Assert.AreEqual(0.5f, r.InfectionTargetedAmp);
        Assert.AreEqual(0.5f, r.VirologyCentroidSpread);
        Assert.AreEqual(0.18f, r.VirologyJitterScale);
        Assert.AreEqual(0.12f, r.VirologyCentroidMutationScale);
        Assert.AreEqual(1.5f, r.VirologyCentroidClamp);
    }

    [Test]
    public void FrameMirrorsHotFields()
    {
        var frame = new SimulationRules.RulesFrame(_rules);
        Assert.AreEqual(_rules.CycleFallbackDt, frame.CycleFallbackDt);
        Assert.AreEqual(_rules.MinTrainAdvantage, frame.MinTrainAdvantage);
        Assert.AreEqual(_rules.BaseMutationRate, frame.BaseMutationRate);
        Assert.AreEqual(_rules.TamedThreshold, frame.TamedThreshold);
        Assert.AreEqual(_rules.InfectionTargetedAmp, frame.InfectionTargetedAmp);
        Assert.AreEqual(_rules.ExecExplorationEpsilon, frame.ExecExplorationEpsilon);
    }

    [Test]
    public void MutationWeightsLeaveRoomForDuplication()
    {
        var r = _rules;
        float used = r.MutationPointWeight + r.MutationInsertWeight + r.MutationDeleteWeight;
        Assert.LessOrEqual(used, 1f);
        Assert.AreEqual(0.15f, 1f - used, 1e-6f);
    }
}
