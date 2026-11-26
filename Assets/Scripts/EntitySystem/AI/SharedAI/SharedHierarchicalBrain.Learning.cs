using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public partial class SharedHierarchicalBrain
{
    private void ConfigureOptimizer()
    {
        float maxGradNorm = SimulationRules.Active.OptimizerMaxGradNorm;
        coordinator.OptimizerMaxGradNorm = maxGradNorm;
        for (int i = 0; i < GoalCount; i++)
            executors[i].OptimizerMaxGradNorm = maxGradNorm;
    }

    private bool ConflictCycle()
    {
        if (_conflictStep == GlobalTrainingSteps) return _conflictCycle;
        _conflictStep = GlobalTrainingSteps;
        ref readonly var frame = ref SimulationRules.Frame;
        _conflictCycle = frame.GradientConflictEnabled && GlobalTrainingSteps % frame.GradientConflictInterval == 0;
        return _conflictCycle;
    }

    public void SetNoveltyProgress(NoveltyProgressSystem system, bool enabled)
    {
        _progress = system;
        _progressNovelty = enabled;
    }

    public void SetLearningGenes(in ColonyGenome genome)
    {
        CompletePending();
        coordinator.SetLearningGenes(in genome);
        for (int g = 0; g < GoalCount; g++) executors[g].SetLearningGenes(in genome);
    }

    public unsafe void SetArm(ColonyArm arm, bool freshReservoir)
    {
        CompletePending();
        NullPolicy = arm == ColonyArm.NullPolicy;

        var rules = SimulationRules.Active;
        var mode  = arm == ColonyArm.BoundedLtc ? LtcMode.Bounded : LtcMode.Legacy;
        coordinator.SetLtc(mode, rules.LtcTauMin, rules.LtcTauMax);
        for (int g = 0; g < GoalCount; g++) executors[g].SetLtc(mode, rules.LtcTauMin, rules.LtcTauMax);
        if (_kernels.IsCreated)
        {
            _kernels[0] = *coordinator.KernelPtr;
            for (int g = 0; g < GoalCount; g++) _kernels[1 + g] = *executors[g].KernelPtr;
        }

        _progressNovelty = arm == ColonyArm.ProgressNovelty;
        if (_progressNovelty && _progress == null) ServiceLocator.Services.TryGet(out _progress);

        if (freshReservoir && arm == ColonyArm.OrthogonalReservoir)
            reservoir.OrthogonalizeRecurrent(rules.ReservoirSpectralRadius);
    }

    public void CompletePending()
    {
        _pending.Complete();
        _pending = default;
        if (!_trainingScheduled) return;
        _trainingScheduled = false;

        coordinator.CompleteTraining();
        for (int g = 0; g < GoalCount; g++)
            executors[g].CompleteTraining();
    }

    public void ApplyPendingGradients()
    {
        CompletePending();
        var   rules = SimulationRules.Active;
        float lr    = ResolveLearningRate(SimulationClock.Time, rules.OptimizerBaseLearningRate, rules.LrDecayPerSecond, rules.MinLearningRate);
        float decay = rules.OptimizerWeightDecay;
        CurrentLearningRate = lr;

        if (ConflictCycle()) MeasureConflict();

        coordinator.ApplyAccumulatedGradients(lr, decay, GlobalDiagnostics);
        for (int i = 0; i < GoalCount; i++)
            executors[i].ApplyAccumulatedGradients(lr, decay, GlobalDiagnostics);

        _rnd.TrainSweep();
        GlobalTrainingSteps++;
    }

    private void MeasureConflict()
    {
        _conflict ??= new GradientConflict();
        _conflict.Begin();
        for (int g = 0; g < GoalCount; g++) executors[g].AccumulateGroupConflict(_conflict);
        _conflict.Finish();
    }

    public static float ResolveLearningRate(float time, float baseLearningRate, float decayPerSecond, float minLearningRate)
    {
        float decayed = baseLearningRate * math.exp(-time * math.max(decayPerSecond, 0f));
        return math.max(math.max(minLearningRate, SimulationRules.LearningRateFloor), decayed);
    }

    public void ReportMeanFitness(float meanFitness)
    {
        var r = SimulationRules.Active;
        if (r.EpsilonScheduleMode != 1) return;
        if (float.IsNaN(_plateauThreshold)) _plateauThreshold = r.EpsilonPlateauFitnessThreshold;
        if (meanFitness < _plateauThreshold) return;

        _plateauScale     = math.max(_plateauScale * r.EpsilonPlateauDrop, r.EpsilonPlateauMinScale);
        _plateauThreshold = _plateauThreshold * r.EpsilonPlateauThresholdGrowth + r.EpsilonPlateauThresholdStep;
    }

    private float ExplorationDecay()
    {
        ref readonly var rules = ref SimulationRules.Frame;
        if (rules.EpsilonScheduleMode == 1) return _plateauScale;

        long steps = coordinator.TotalTrainingSteps;
        for (int g = 0; g < GoalCount; g++) steps += executors[g].TotalTrainingSteps;
        return math.exp(-(float)steps * rules.EpsilonDecayPerStep);
    }

    public void GiveReward(float reward, in BrainDecision decision, EntityBrainContext ctx)
    {
        var mlp  = ctx.ExecMLPs[(int)decision.Goal];
        var item = mlp.Decisions.Find(decision.ExecDecisionId);
        if (item == null)
        {
            mlp.Diagnostics.RecordDroppedReward();
            GlobalDiagnostics.RecordDroppedReward();
            return;
        }

        var   rules  = SimulationRules.Active;
        float scaled = _rewardNorm.UpdateAndScale(reward, rules.RewardClipSigma, rules.RewardStdFloor);

        item.Evaluation    = scaled;
        item.RewardApplied = true;
        mlp.Diagnostics.RecordRewardLatency(SimulationClock.Time - item.StartTime);
        GlobalDiagnostics.RecordAppliedReward();

        if (decision.CoordDecisionId != ctx.CoordDecisionId) return;

        ctx.GoalTotalReward      += scaled;
        ctx.GoalDiscountedReturn += scaled * DelayedPerceptron.DiscountTime(
            ctx.Gamma(SimulationRules.Frame.CoordGammaPerSecond), SimulationClock.Time - ctx.GoalStartTime);
        ctx.GoalRewardCount++;
    }

    public bool TryAddReward(float reward, in BrainDecision decision, EntityBrainContext ctx)
    {
        var item = ctx.ExecMLPs[(int)decision.Goal].Decisions.Find(decision.ExecDecisionId);
        if (item == null || item.Trained) return false;

        var rules = SimulationRules.Active;
        item.Evaluation += _rewardNorm.Scale(reward, rules.RewardClipSigma, rules.RewardStdFloor);
        return true;
    }

    private void ReadIntrinsic(int count)
    {
        for (int k = 0; k < count; k++) _rndReward[k] = _rnd.IntrinsicReward(k);
        if (_progressNovelty && _progress != null) ApplyProgress(count);
    }

    private void ApplyProgress(int count)
    {
        ref readonly var frame = ref SimulationRules.Frame;
        float fastRate = frame.NoveltyFastRate;
        float slowRate = frame.NoveltySlowRate;
        for (int k = 0; k < count; k++)
        {
            var ctx = _rndCtx[k];
            if (ctx == null) continue;
            _rndReward[k] = _progress.Step(ctx.NoveltyIndex, _rndReward[k], fastRate, slowRate);
        }
    }

    private void AccumulateNovelty(int count)
    {
        for (int k = 0; k < count; k++)
        {
            var ctx = _rndCtx[k];
            if (ctx == null || ctx.CoordDecisionId == 0) continue;
            ctx.NoveltySum += _rndReward[k];
            ctx.NoveltyCount++;
        }
    }

    public void CompleteDecision(in BrainDecision decision, float reward, EntityBrainContext ctx,
        float simTime, EntityCommandStatus status)
    {
        GiveReward(reward, in decision, ctx);

        ctx.ExecMLPs[(int)decision.Goal].Diagnostics.RecordCompletion(
            simTime - decision.StartTime, status == EntityCommandStatus.Interrupted);

        if (ctx.ExecWindow.DecisionId == decision.ExecDecisionId) ctx.ExecWindow.End();
    }

    public void CompleteTerminal(EntityBrainContext ctx, float penalty, float deathTime)
    {
        if (ctx == null) return;
        CompletePending();

        var   rules  = SimulationRules.Active;
        float scaled = _rewardNorm.Scale(penalty, rules.RewardClipSigma, rules.RewardStdFloor);

        int g = (int)ctx.CurrentGoal;

        var running = ctx.ExecWindow.DecisionId != 0
            ? ctx.ExecMLPs[g].Decisions.Find(ctx.ExecWindow.DecisionId)
            : null;

        if (running != null && !running.RewardApplied)
        {
            running.Evaluation    = 0f;
            running.RewardApplied = true;
        }

        ctx.ExecWindow.End();

        ref readonly var frame = ref SimulationRules.Frame;
        int   replay  = frame.DeathReplayCount;
        int   passes  = frame.DeathReplayPasses;
        float weight  = frame.DeathReplayWeight;
        for (int i = 0; i < GoalCount; i++)
            executors[i].FlushTerminal(ctx.ExecMLPs[i], execCritics[i], ctx.Gamma(frame.ExecGammaPerSecond),
                                       scaled, deathTime, replay, passes, weight);

        FlushGoalRewardToCoordinator(ctx);
        coordinator.FlushTerminal(ctx.CoordMLP, coordCritic, ctx.Gamma(frame.CoordGammaPerSecond), scaled, deathTime,
                                  replay, passes, weight);

        ctx.CoordDecisionId = 0;
        ctx.GoalEndTime     = 0f;
        ctx.BeginGoal(0f);
    }

    public static float EnergySurplus(float energyNorm, float floor)
        => math.saturate((energyNorm - floor) / math.max(1f - floor, 1e-3f));

    public static float RarityTerm(float weight, float surplus, float share)
        => weight * surplus / math.sqrt(1f + share * PlanCatalog.Count);

    private void FlushGoalRewardToCoordinator(EntityBrainContext ctx)
    {
        if (ctx.CoordDecisionId == 0) return;

        var item = ctx.CoordMLP.Decisions.Find(ctx.CoordDecisionId);
        if (item == null) return;

        var r = SimulationRules.Active;
        ctx.GoalNovelty = ctx.NoveltyCount > 0 ? ctx.NoveltySum / ctx.NoveltyCount : 0f;
        ref readonly var frame = ref SimulationRules.Frame;
        float surplus = EnergySurplus(ctx.EnergyNorm, frame.CuriosityEnergyFloor);
        float curiosity = r.CoordCuriosityWeight * ExplorationDecay()
                        * (1f - r.CuriosityStorageDamp * math.saturate(ctx.ColonyStorage))
                        * (1f + frame.CuriosityEnergyGain * surplus);
        float goal = ctx.GoalDiscountedReturn
                   + ctx.GoalBonus
                   + r.GoalFoodWeight   * ctx.GoalFoodEaten
                   + r.DigestRewardWeight * ctx.GoalDigested
                   + r.GoalEnergyWeight * (ctx.EnergyNorm - ctx.GoalStartEnergy)
                   + r.GoalRestWeight   * ctx.GoalRestScore
                   + curiosity * ctx.GoalNovelty
                   + RarityTerm(frame.CuriosityRarityWeight, surplus, ctx.EndedPlanShare);

        float clip = r.RewardClipSigma;
        item.Evaluation    = math.clamp(goal, -clip, clip);
        item.RewardApplied = true;
        ctx.Surprise = math.lerp(ctx.Surprise, math.abs(item.Evaluation - item.ValueEstimate), SimulationRules.Frame.SurpriseAlpha);
    }
}