using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class SharedHierarchicalBrain : IDisposable
{
    private const float OptimizerMaxGradNorm = 3f;
    public enum Goal { Survive = 0, Hunt = 1, Forage = 2, Social = 3 }
    public const int GoalCount = 4;

    public static readonly int[][] ActionSubsets = ActionCatalog.BuildSubsets(GoalCount);

    private const int CoordDelaySteps  = 10;
    private const int ExecDelaySteps   = 3;

    public const int PlanCount = PlanCatalog.Count;

    public const int CoordAux = 2;

    private static int[] BuildCoordSizes(int combined) => new[] { combined, 32, 16, 16, PlanCount + 1 + CoordAux };

    public const int HeadingOutputs   = 2;
    public const int CurvatureOutputs = 1;
    public const int SpeechOutputs    = 4;
    public const int CurvatureAux     = HeadingOutputs;
    public const int SpeechAux        = HeadingOutputs + CurvatureOutputs;

    public static int ExecAux(int goal)
        => HeadingOutputs + CurvatureOutputs + (goal == (int)Goal.Social ? SpeechOutputs : 0);

    private static int[] BuildExecSizes(int combined, int goal)
        => new[] { combined, 64, 32, 32, ActionSubsets[goal].Length + 1 + ExecAux(goal) };


    private readonly LSTMMemory          reservoir;
    private readonly DelayedPerceptron   coordinator;
    private readonly DelayedPerceptron[] executors;

    internal LSTMMemory          Reservoir   => reservoir;
    internal DelayedPerceptron   Coordinator => coordinator;
    internal DelayedPerceptron[] Executors   => executors;

    private readonly ValueCritic   coordCritic;
    private readonly ValueCritic[] execCritics;

    public readonly int   InputSize;
    public readonly int   LstmHidden;
    public readonly int[] CoordLayerSizes;
    public readonly int[][] ExecLayerSizes;

    private readonly RandomNetworkDistillation _rnd;

    public readonly BrainDiagnostics GlobalDiagnostics = new();
    public int GlobalTrainingSteps { get; private set; }

    private const float OptimizerBaseLr      = 0.01f;
    private const float OptimizerLrDecay     = 2e-5f;
    private const float OptimizerWeightDecay = 1e-5f;
    private RunningMeanStd _rewardNorm;

    private PerceptronLayout   _coordCtxLayout;
    private PerceptronLayout[] _execCtxLayouts;
    

    public SharedHierarchicalBrain(int inputSize, int lstmHidden = 32)
    {
        InputSize  = inputSize;
        LstmHidden = lstmHidden;
        int combined = inputSize + lstmHidden;

        CoordLayerSizes = BuildCoordSizes(combined);
        ExecLayerSizes  = new int[GoalCount][];
        for (int i = 0; i < GoalCount; i++)
            ExecLayerSizes[i] = BuildExecSizes(combined, i);

        reservoir   = new LSTMMemory(inputSize, lstmHidden, SimulationRules.Active.ReservoirSpectralRadius);
        coordinator = new DelayedPerceptron(CoordLayerSizes);

        executors = new DelayedPerceptron[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            executors[i] = new DelayedPerceptron(ExecLayerSizes[i]);

        coordCritic = new ValueCritic(combined);
        execCritics = new ValueCritic[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            execCritics[i] = new ValueCritic(combined);

        _rnd = new RandomNetworkDistillation(inputSize);

        ConfigureOptimizer();
        BuildContextLayouts();
        ApplyPendingGradients();
    }

    private void ConfigureOptimizer()
    {
        coordinator.OptimizerMaxGradNorm = OptimizerMaxGradNorm;
        for (int i = 0; i < GoalCount; i++)
            executors[i].OptimizerMaxGradNorm = OptimizerMaxGradNorm;
    }

    private const int TruncWindow       = 8;
    private const int DecisionRingSlack = 2;
    private const int CoordRingCapacity = CoordDelaySteps + DecisionRingSlack;
    private const int ExecRingCapacity  = ExecDelaySteps  + DecisionRingSlack;

    public float CurrentLearningRate { get; private set; } = OptimizerBaseLr;

    private JobHandle _pending;
    private bool      _trainingScheduled;

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
        var   rules   = SimulationRules.Active;
        float decayed = OptimizerBaseLr * math.exp(-SimulationClock.Time * rules.LrDecayPerSecond);
        float lr      = math.max(rules.MinLearningRate, decayed);
        CurrentLearningRate = lr;

        coordinator.ApplyAccumulatedGradients(lr, OptimizerWeightDecay, GlobalDiagnostics);
        for (int i = 0; i < GoalCount; i++)
            executors[i].ApplyAccumulatedGradients(lr, OptimizerWeightDecay, GlobalDiagnostics);

        _rnd.TrainSweep();
        GlobalTrainingSteps++;
    }

    private float _plateauScale = 1f;
    private float _plateauThreshold = float.NaN;

    public float ExplorationScale => _plateauScale;

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

    public EntityBrainContext CreateContext(GeneticParameters? p = null)
    {
        CompletePending();
        if (_coordCtxLayout == null) BuildContextLayouts();
        return new EntityBrainContext(InputSize, LstmHidden, _coordCtxLayout, _execCtxLayouts,
                                      p ?? GeneticParameters.Default);
    }
    private void BuildContextLayouts()
    {
        _coordCtxLayout = coordinator.BuildContextLayout(TruncWindow, CoordRingCapacity, CoordAux);
        _execCtxLayouts = new PerceptronLayout[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            _execCtxLayouts[i] = executors[i].BuildContextLayout(TruncWindow, ExecRingCapacity, ExecAux(i));
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
            SimulationRules.Frame.CoordGammaPerSecond, SimulationClock.Time - ctx.GoalStartTime);
        ctx.GoalRewardCount++;
    }

    public SharedHierarchicalBrain(SharedHierarchicalBrain src) : this(src.InputSize, src.LstmHidden)
    {
        src.CompletePending();
        reservoir.Dispose();
        coordinator.Dispose();
        reservoir   = new LSTMMemory(src.reservoir);
        coordinator = new DelayedPerceptron(src.coordinator);
        for (int i = 0; i < GoalCount; i++)
        {
            executors[i].Dispose();
            executors[i] = new DelayedPerceptron(src.executors[i]);
        }

        ConfigureOptimizer();
        BuildContextLayouts();
        ApplyPendingGradients();
    }

    private static readonly float[] GoalMinDuration = BuildGoalDurations(false);
    private static readonly float[] GoalMaxDuration = BuildGoalDurations(true);
    private static readonly int     BiasStride      = BuildBiasStride();

    private static float[] BuildGoalDurations(bool max)
    {
        var r = new float[GoalCount];
        for (int g = 0; g < GoalCount; g++)
        {
            var subset = ActionSubsets[g];
            float v = max ? 0f : float.MaxValue;
            for (int i = 0; i < subset.Length; i++)
                v = max ? math.max(v, ActionDurationTable.Max(subset[i]))
                        : math.min(v, ActionDurationTable.Min(subset[i]));
            r[g] = v;
        }
        return r;
    }

    private static int BuildBiasStride()
    {
        int s = PlanCount;
        for (int g = 0; g < GoalCount; g++) s = math.max(s, ActionSubsets[g].Length);
        return s;
    }

    private PerceptronBatch _batch;
    private readonly GoalBuckets _buckets = new();
    private NativeArray<KernelLayout> _kernels;
    private NativeArray<NetWeights>   _nets;

    private EntityBrainContext[] _dCtx      = Array.Empty<EntityBrainContext>();
    private float[]              _dTime     = Array.Empty<float>();
    private float[]              _dDt       = Array.Empty<float>();
    private int[]                _dRow      = Array.Empty<int>();
    private BrainDecision[]      _dDecision = Array.Empty<BrainDecision>();
    private bool[]               _dMade     = Array.Empty<bool>();
    private NativeList<int>      _rndRows;
    private int _dCount;
    private int _rCount;

    public void BeginDecisionBatch()
    {
        CompletePending();
        for (int i = 0; i < _dCount; i++) _dCtx[i] = null;
        _dCount = 0;
        _rCount = 0;
        _batch?.ClearReservoir();
        if (_rndRows.IsCreated) _rndRows.Clear();
    }

    public unsafe int EnqueueDecision(EntityBrainContext ctx, float[] input, float simTime, float dt, bool allowDecision = true)
    {
        _batch ??= new PerceptronBatch(InputSize, BiasStride);

        int row = _rCount++;
        _batch.SetInput(row, input);
        _batch.AddReservoir(BuildReservoirItem(ctx.Reservoir, row, simTime, dt));

        if (!allowDecision || ctx.ExecWindow.IsRunning(simTime)) return -1;

        int d = _dCount;
        EnsureDeciderCapacity(d + 1);
        if (!_rndRows.IsCreated) _rndRows = new NativeList<int>(64, Allocator.Persistent);

        _dCtx[d]  = ctx;
        _dTime[d] = simTime;
        _dDt[d]   = dt;
        _dRow[d]  = row;
        _dMade[d] = false;
        _rndRows.Add(row);
        _rnd.Collect(input);

        _dCount = d + 1;
        return d;
    }

    private static unsafe ReservoirItem BuildReservoirItem(LSTMContext res, int row, float simTime, float dt)
    {
        var item = new ReservoirItem { State = res.Ptr, Ema = res.Ema, InputRow = row, Alpha = 1f, Step = 1 };

        float hz = SimulationRules.Frame.ReservoirHz;
        if (hz <= 0f)
        {
            res.EmaPrimed = false;
            return item;
        }

        item.UseEma = 1;
        item.Alpha  = res.EmaPrimed ? 1f - math.exp(-math.max(dt, 0f) * hz) : 1f;
        res.EmaPrimed = true;

        if (simTime < res.NextStepTime)
        {
            item.Step = 0;
            return item;
        }

        float next = res.NextStepTime + 1f / hz;
        res.NextStepTime = next > simTime ? next : simTime + 1f / hz;
        return item;
    }

    public bool TryGetDecision(int slot, out BrainDecision decision)
    {
        if ((uint)slot < (uint)_dCount && _dMade[slot])
        {
            decision = _dDecision[slot];
            return true;
        }
        decision = default;
        return false;
    }

    public unsafe void RunDecisions(float coordEps = 0.05f, float execEps = 0.15f)
    {
        CompletePending();

        if (_rCount == 0)
        {
            ScheduleTraining();
            return;
        }

        EnsureNativeTables();
        var batch = _batch;
        int n = _dCount;

        var reservoirHandle = batch.ScheduleReservoir(reservoir.WeightsPtr, reservoir.BiasesPtr, LstmHidden);

        if (n == 0)
        {
            _pending = reservoirHandle;
            ScheduleTraining();
            return;
        }

        var rndHandle = _rnd.ScheduleInfer(batch.Inputs, _rndRows.AsArray(), n);

        float decay = ExplorationDecay();
        ref readonly var frame = ref SimulationRules.Frame;
        float coordGamma = frame.CoordGammaPerSecond;
        float execGamma  = frame.ExecGammaPerSecond;

        float maskBias = frame.PlanMaskBias;

        batch.ClearItems();
        for (int d = 0; d < n; d++)
        {
            var ctx = _dCtx[d];
            ctx.SkipExec = false;
            if (_dTime[d] < ctx.GoalEndTime) continue;

            FlushGoalRewardToCoordinator(ctx);

            var row = batch.BiasRow(d);
            row.Clear();
            for (int p = 0; p < PlanCount; p++)
                row[p] = ctx.CoordBias[p]
                       + (PlanCatalog.IsFeasible((PlanKind)p, ctx.PlanHints, ctx.CasteMask) ? 0f : -maskBias);

            var mlp  = ctx.CoordMLP;
            int slot = coordinator.BeginForward(mlp, _dDt[d]);

            batch.Add(new ForwardItem
            {
                Ctx         = mlp.Ptr,
                Lstm        = ctx.Reservoir.Ptr,
                Net         = 0,
                Slot        = slot,
                InputRow    = _dRow[d],
                BiasRow     = d,
                Owner       = d,
                Dt          = mlp.DeltaTime,
                Temperature = mlp.Params.SoftmaxTemperature,
            });
        }

        int coordCount = batch.Count;
        var coordHandle = coordCount > 0
            ? batch.Schedule(_kernels, _nets, LstmHidden, reservoirHandle)
            : reservoirHandle;

        JobHandle.CombineDependencies(coordHandle, rndHandle).Complete();

        for (int d = 0; d < n; d++) _dCtx[d].IntrinsicReward = _rnd.IntrinsicReward(d);

        for (int k = 0; k < coordCount; k++)
        {
            var item = batch.ItemAt(k);
            int d    = item.Owner;
            var ctx  = _dCtx[d];
            float simTime = _dTime[d];

            ctx.CoordMLP.Activation(0).CopyTo(ctx.CoordCombined);

            var planTicket = coordinator.FinishDecide(ctx.CoordCombined, ctx.CoordMLP, item.Slot,
                batch.BiasRow(d).Slice(0, PlanCount), CoordDelaySteps, coordCritic, coordGamma, simTime,
                frame.PlanMinSeconds, frame.PlanMaxSeconds, coordEps, decay, false);

            var plan = (PlanKind)planTicket.Predicted;
            ctx.CoordAux = new float2(planTicket.AuxValue[0], planTicket.AuxValue[1]);
            for (int p = 0; p < PlanCount; p++) ctx.PlanProbs[p] = planTicket.Probs[p];
            ctx.CurrentPlan     = plan;
            ctx.CurrentGoal     = PlanCatalog.GoalOf(plan);
            ctx.CoordDecisionId = planTicket.DecisionId;
            ctx.GoalEndTime     = simTime + planTicket.Duration;
            ctx.BeginGoal(simTime);
        }

        for (int d = 0; d < n; d++)
        {
            var ctx = _dCtx[d];
            ctx.ExecMask = PlanCatalog.EntryMask(ctx.CurrentPlan, ctx.PlanHints) & ctx.CasteMask;
            if (ctx.ExecMask != 0 && ctx.CurrentPlan < PlanKind.Count) continue;

            ctx.SkipExec    = true;
            ctx.GoalBonus  += frame.PlanInfeasibleReward;
            ctx.GoalEndTime = 0f;
        }

        batch.ClearItems();
        _buckets.Build(_dCtx, n);

        for (int k = 0; k < _buckets.Count; k++)
        {
            int d   = _buckets.At(k);
            var ctx = _dCtx[d];
            int g   = (int)ctx.CurrentGoal;

            var subset  = ActionSubsets[g];
            var row     = batch.BiasRow(d);
            int allowed = 0;
            row.Clear();
            for (int j = 0; j < subset.Length; j++)
            {
                if ((ctx.ExecMask & (1 << subset[j])) != 0) allowed++;
                else row[j] = -maskBias;
            }

            if (allowed == 0)
            {
                ctx.SkipExec    = true;
                ctx.GoalBonus  += frame.PlanInfeasibleReward;
                ctx.GoalEndTime = 0f;
                continue;
            }
            ctx.ExecForced = allowed == 1;

            var mlp  = ctx.ExecMLPs[g];
            int slot = executors[g].BeginForward(mlp, _dDt[d]);

            batch.Add(new ForwardItem
            {
                Ctx         = mlp.Ptr,
                Lstm        = ctx.Reservoir.Ptr,
                Net         = 1 + g,
                Slot        = slot,
                InputRow    = _dRow[d],
                BiasRow     = d,
                Owner       = d,
                Dt          = mlp.DeltaTime,
                Temperature = mlp.Params.SoftmaxTemperature,
            });
        }

        int execCount = batch.Count;
        if (execCount > 0) batch.Schedule(_kernels, _nets, LstmHidden).Complete();

        for (int k = 0; k < execCount; k++)
        {
            var item = batch.ItemAt(k);
            int d    = item.Owner;
            int g    = item.Net - 1;
            var ctx  = _dCtx[d];
            float simTime = _dTime[d];

            var combined = ctx.ExecCombined[g];
            ctx.ExecMLPs[g].Activation(0).CopyTo(combined);

            var ticket = executors[g].FinishDecide(combined, ctx.ExecMLPs[g], item.Slot,
                batch.BiasRow(d).Slice(0, ActionSubsets[g].Length), ExecDelaySteps, execCritics[g], execGamma,
                simTime, GoalMinDuration[g], GoalMaxDuration[g], execEps, decay, ctx.ExecForced);

            int action    = ActionSubsets[g][ticket.Predicted];
            float clamped = math.clamp(ticket.Duration,
                ActionDurationTable.Min(action), ActionDurationTable.Max(action));
            ticket.Duration = clamped;

            ctx.ExecWindow.Begin(ticket.DecisionId, simTime, math.max(clamped, ctx.GoalEndTime - simTime));

            var aux = ticket.AuxValue;
            float4 speech = g == (int)Goal.Social
                ? new float4(aux[SpeechAux], aux[SpeechAux + 1], aux[SpeechAux + 2], aux[SpeechAux + 3])
                : float4.zero;

            _dDecision[d] = new BrainDecision(action, ticket.DecisionId, ctx.CoordDecisionId,
                ctx.CurrentGoal, simTime, clamped, new float2(ticket.HeadingSin, ticket.HeadingCos),
                aux[CurvatureAux], speech, ctx.CurrentPlan, ctx.GoalEndTime);
            _dMade[d] = true;
        }

        ScheduleTraining();
    }

    private void ScheduleTraining()
    {
        float clip = SimulationRules.Frame.PpoClipEpsilon;

        var handle = coordinator.ScheduleTraining(clip);
        for (int g = 0; g < GoalCount; g++)
            handle = JobHandle.CombineDependencies(handle, executors[g].ScheduleTraining(clip));

        _pending = JobHandle.CombineDependencies(_pending, handle);
        _trainingScheduled = true;
        JobHandle.ScheduleBatchedJobs();
    }

    private unsafe void EnsureNativeTables()
    {
        coordinator.EnsureNative();
        reservoir.EnsureNative();
        for (int g = 0; g < GoalCount; g++)
            executors[g].EnsureNative();

        if (!_kernels.IsCreated)
        {
            _kernels = new NativeArray<KernelLayout>(GoalCount + 1, Allocator.Persistent);
            _nets    = new NativeArray<NetWeights>(GoalCount + 1, Allocator.Persistent);

            _kernels[0] = *coordinator.KernelPtr;
            for (int g = 0; g < GoalCount; g++)
                _kernels[1 + g] = *executors[g].KernelPtr;
        }

        _nets[0] = new NetWeights { W = coordinator.WeightsPtr, B = coordinator.BiasesPtr };
        for (int g = 0; g < GoalCount; g++)
            _nets[1 + g] = new NetWeights { W = executors[g].WeightsPtr, B = executors[g].BiasesPtr };
    }

    private void EnsureDeciderCapacity(int needed)
    {
        if (needed <= _dCtx.Length) return;
        int cap = Math.Max(_dCtx.Length << 1, needed);
        Array.Resize(ref _dCtx, cap);
        Array.Resize(ref _dTime, cap);
        Array.Resize(ref _dDt, cap);
        Array.Resize(ref _dRow, cap);
        Array.Resize(ref _dDecision, cap);
        Array.Resize(ref _dMade, cap);
    }

    public void Dispose()
    {
        CompletePending();
        coordinator.Dispose();
        reservoir.Dispose();
        for (int g = 0; g < GoalCount; g++)
            executors[g].Dispose();
        _rnd.Dispose();
        if (_rndRows.IsCreated) _rndRows.Dispose();
        _batch?.Dispose();
        _batch = null;
        if (_kernels.IsCreated) _kernels.Dispose();
        if (_nets.IsCreated)    _nets.Dispose();
    }

    public void CompleteDecision(in BrainDecision decision, float reward, EntityBrainContext ctx,
        float simTime, EntityCommandStatus status)
    {
        GiveReward(reward, in decision, ctx);

        ctx.ExecMLPs[(int)decision.Goal].Diagnostics.RecordCompletion(
            simTime - decision.StartTime, status == EntityCommandStatus.Interrupted);

        if (ctx.ExecWindow.DecisionId == decision.ExecDecisionId) ctx.ExecWindow.End();
    }

    public void CompleteTerminal(EntityBrainContext ctx, float penalty)
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
            executors[i].FlushTerminal(ctx.ExecMLPs[i], execCritics[i], frame.ExecGammaPerSecond,
                                       i == g ? scaled : 0f, replay, passes, weight);

        FlushGoalRewardToCoordinator(ctx);
        coordinator.FlushTerminal(ctx.CoordMLP, coordCritic, frame.CoordGammaPerSecond, scaled,
                                  replay, passes, weight);

        ctx.CoordDecisionId = 0;
        ctx.GoalEndTime     = 0f;
        ctx.BeginGoal(0f);
    }

    private void FlushGoalRewardToCoordinator(EntityBrainContext ctx)
    {
        if (ctx.CoordDecisionId == 0) return;

        var item = ctx.CoordMLP.Decisions.Find(ctx.CoordDecisionId);
        if (item == null) return;

        var r = SimulationRules.Active;
        float curiosity = r.CoordCuriosityWeight * ExplorationDecay()
                        * (1f - r.CuriosityStorageDamp * math.saturate(ctx.ColonyStorage));
        float goal = ctx.GoalDiscountedReturn
                   + ctx.GoalBonus
                   + r.GoalFoodWeight   * ctx.GoalFoodEaten
                   + r.GoalEnergyWeight * (ctx.EnergyNorm - ctx.GoalStartEnergy)
                   + r.GoalRestWeight   * ctx.GoalRestCount
                   + curiosity * ctx.GoalNovelty;

        float clip = r.RewardClipSigma;
        item.Evaluation    = Mathf.Clamp(goal, -clip, clip);
        item.RewardApplied = true;
    }

    public float GetCoordinatorEntropy(EntityBrainContext ctx) => ctx.CoordMLP.AverageEntropy;
    public Goal  GetCurrentGoal(EntityBrainContext ctx)        => ctx.CurrentGoal;

    public float GetExecutorEntropy(Goal goal, EntityBrainContext ctx)
        => ctx.ExecMLPs[(int)goal].AverageEntropy;


    private SharedHierarchicalBrain(LSTMMemory reservoir, DelayedPerceptron coordinator, DelayedPerceptron[] executors)
    {
        InputSize  = reservoir.InputSize;
        LstmHidden = reservoir.HiddenSize;

        this.reservoir   = reservoir;
        this.coordinator = coordinator;
        this.executors   = executors;

        CoordLayerSizes = coordinator.LayerSizes;
        ExecLayerSizes  = new int[GoalCount][];
        for (int i = 0; i < GoalCount; i++)
            ExecLayerSizes[i] = executors[i].LayerSizes;

        int combined = InputSize + LstmHidden;
        coordCritic = new ValueCritic(combined);
        execCritics = new ValueCritic[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            execCritics[i] = new ValueCritic(combined);

        _rnd = new RandomNetworkDistillation(InputSize);

        ConfigureOptimizer();
        BuildContextLayouts();
        ApplyPendingGradients();
    }

    internal static SharedHierarchicalBrain FromModels(
        LSTMMemory reservoir, DelayedPerceptron coordinator, DelayedPerceptron[] executors)
    {
        if (reservoir == null || coordinator == null || executors == null || executors.Length != GoalCount)
        {
            Debug.LogError("Снапшот мозга некорректен: состав моделей не совпадает");
            return null;
        }

        for (int i = 0; i < GoalCount; i++)
        {
            if (executors[i] == null)
            {
                Debug.LogError($"Снапшот мозга некорректен: exec-модель {i} отсутствует");
                return null;
            }

            int[] sizes  = executors[i].LayerSizes;
            int actual   = sizes[sizes.Length - 1];
            int expected = ActionSubsets[i].Length + 1 + ExecAux(i);
            if (actual != expected)
            {
                Debug.LogError($"Снапшот мозга несовместим: exec {i} имеет {actual} выходов, ожидалось {expected} (directional head)");
                return null;
            }
        }

        return new SharedHierarchicalBrain(reservoir, coordinator, executors);
    }

    public SharedBrainSnapshot ToSnapshot()
    {
        CompletePending();
        return new SharedBrainSnapshot(this);
    }

    public static SharedHierarchicalBrain FromSnapshot(byte[] data, int inputSize, int lstmHidden)
        => FromSnapshot(SharedBrainSnapshot.Deserialize(data), inputSize, lstmHidden);

    public static SharedHierarchicalBrain FromSnapshot(SharedBrainSnapshot snapshot, int inputSize, int lstmHidden)
    {
        if (snapshot == null || snapshot.Brain == null) return null;

        if (!snapshot.Brain.MatchesArchitecture(inputSize, lstmHidden))
        {
            Debug.LogError($"Архитектура снапшота ({snapshot.Brain.InputSize}/{snapshot.Brain.LstmHidden}) " +
                           $"не совпадает с текущей ({inputSize}/{lstmHidden})");
            return null;
        }

        return snapshot.Brain;
    }

    public bool MatchesArchitecture(int inputSize, int lstmHidden)
    {
        if (InputSize != inputSize || LstmHidden != lstmHidden) return false;

        int combined = inputSize + lstmHidden;
        if (!SizesMatch(CoordLayerSizes, BuildCoordSizes(combined))) return false;

        for (int i = 0; i < GoalCount; i++)
            if (!SizesMatch(ExecLayerSizes[i], BuildExecSizes(combined, i))) return false;

        return true;
    }
    private static bool SizesMatch(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}