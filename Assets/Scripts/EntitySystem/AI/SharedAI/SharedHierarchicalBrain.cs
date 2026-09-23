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

    public static readonly int[][] ActionSubsets =
    {
        //  Survive: Idle, Wander, Flee, Eat, Rest
        new[] { 0, 1, 5, 6, 10 },
        //  Hunt: Idle, Wander, Attack, Flee, Threaten
        new[] { 0, 1, 4, 5, 11 },
        //  Forage: Wander, RememberPoint, GoToPoint, Eat
        new[] { 1, 2, 3, 6 },
        //  Social: Idle, Wander, Reproduce, Speech, Mimic, ShareFood
        new[] { 0, 1, 7, 8, 9, 12 },
    };

    private const int CoordDelaySteps  = 10;
    private const int ExecDelaySteps   = 3;

    private static int[] BuildCoordSizes(int combined) => new[] { combined, 32, 16, 16, GoalCount + 1 };

    public const int HeadingOutputs = 2;

    private static int[] BuildExecSizes(int combined, int goal)
        => new[] { combined, 64, 32, 32, ActionSubsets[goal].Length + 1 + HeadingOutputs };


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
    private const float CuriosityWeight = 0.15f;
    private const float Gamma = 0.95f;

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

    public void ApplyPendingGradients()
    {
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
        if (_coordCtxLayout == null) BuildContextLayouts();
        return new EntityBrainContext(InputSize, LstmHidden, _coordCtxLayout, _execCtxLayouts,
                                      p ?? GeneticParameters.Default);
    }
    private void BuildContextLayouts()
    {
        _coordCtxLayout = coordinator.BuildContextLayout(TruncWindow, CoordRingCapacity, 0);
        _execCtxLayouts = new PerceptronLayout[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            _execCtxLayouts[i] = executors[i].BuildContextLayout(TruncWindow, ExecRingCapacity, HeadingOutputs);
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
        float shaped = reward + ctx.IntrinsicReward * CuriosityWeight;
        float scaled = _rewardNorm.UpdateAndScale(shaped, rules.RewardClipSigma, rules.RewardStdFloor);

        item.Evaluation    = scaled;
        item.RewardApplied = true;
        mlp.Diagnostics.RecordRewardLatency(SimulationClock.Time - item.StartTime);
        GlobalDiagnostics.RecordAppliedReward();

        if (decision.CoordDecisionId != ctx.CoordDecisionId) return;

        ctx.GoalTotalReward      += scaled;
        ctx.GoalDiscountedReturn += scaled * ctx.GoalDiscountFactor;
        ctx.GoalDiscountFactor   *= Gamma;
        ctx.GoalRewardCount++;
    }

    public SharedHierarchicalBrain(SharedHierarchicalBrain src) : this(src.InputSize, src.LstmHidden)
    {
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

    private const int FleeAction = 5;
    private static readonly int[] FleeSlots = BuildFleeSlots();

    private static int[] BuildFleeSlots()
    {
        var slots = new int[GoalCount];
        for (int g = 0; g < GoalCount; g++)
        {
            slots[g] = -1;
            var subset = ActionSubsets[g];
            for (int i = 0; i < subset.Length; i++)
                if (subset[i] == FleeAction) { slots[g] = i; break; }
        }
        return slots;
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
        int s = GoalCount;
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
        for (int i = 0; i < _dCount; i++) _dCtx[i] = null;
        _dCount = 0;
        _rCount = 0;
        _batch?.ClearReservoir();
        if (_rndRows.IsCreated) _rndRows.Clear();
    }

    public unsafe int EnqueueDecision(EntityBrainContext ctx, float[] input, float simTime, float dt)
    {
        _batch ??= new PerceptronBatch(InputSize, BiasStride);

        int row = _rCount++;
        _batch.SetInput(row, input);
        _batch.AddReservoir(ctx.Reservoir.Ptr, row);

        if (ctx.ExecWindow.IsRunning(simTime)) return -1;

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
        if (_rCount == 0)
        {
            RunTraining();
            return;
        }

        EnsureNativeTables();
        var batch = _batch;
        int n = _dCount;

        var pre = batch.ScheduleReservoir(reservoir.WeightsPtr, reservoir.BiasesPtr, LstmHidden);
        if (n > 0) pre = JobHandle.CombineDependencies(pre, _rnd.ScheduleInfer(batch.Inputs, _rndRows.AsArray(), n));
        pre.Complete();

        if (n == 0)
        {
            RunTraining();
            return;
        }

        float decay = ExplorationDecay();
        for (int d = 0; d < n; d++) _dCtx[d].IntrinsicReward = _rnd.IntrinsicReward(d);

        batch.ClearItems();
        for (int d = 0; d < n; d++)
        {
            var ctx = _dCtx[d];
            if (_dTime[d] < ctx.GoalEndTime) continue;

            FlushGoalRewardToCoordinator(ctx);

            int biasRow = -1;
            if (HasBias(ctx.CoordBias))
            {
                var row = batch.BiasRow(d);
                row.Clear();
                ctx.CoordBias.AsSpan().CopyTo(row);
                biasRow = d;
            }

            var mlp  = ctx.CoordMLP;
            int slot = coordinator.BeginForward(mlp, _dDt[d]);

            batch.Add(new ForwardItem
            {
                Ctx         = mlp.Ptr,
                Lstm        = ctx.Reservoir.Ptr,
                Net         = 0,
                Slot        = slot,
                InputRow    = _dRow[d],
                BiasRow     = biasRow,
                Owner       = d,
                Dt          = mlp.DeltaTime,
                Temperature = mlp.Params.SoftmaxTemperature,
            });
        }

        int coordCount = batch.Count;
        if (coordCount > 0)
        {
            batch.Schedule(_kernels, _nets, LstmHidden).Complete();

            for (int k = 0; k < coordCount; k++)
            {
                var item = batch.ItemAt(k);
                int d    = item.Owner;
                var ctx  = _dCtx[d];
                float simTime = _dTime[d];

                ctx.CoordMLP.Activation(0).CopyTo(ctx.CoordCombined);

                var goalTicket = coordinator.FinishDecide(ctx.CoordCombined, ctx.CoordMLP, item.Slot,
                    item.BiasRow >= 0, CoordDelaySteps, coordCritic, Gamma, simTime,
                    ActionDurationTable.MinGoalSeconds, ActionDurationTable.MaxGoalSeconds, coordEps, decay);

                ctx.CurrentGoal          = (Goal)goalTicket.Predicted;
                ctx.CoordDecisionId      = goalTicket.DecisionId;
                ctx.GoalEndTime          = simTime + goalTicket.Duration;
                ctx.GoalTotalReward      = 0f;
                ctx.GoalDiscountedReturn = 0f;
                ctx.GoalDiscountFactor   = 1f;
                ctx.GoalRewardCount      = 0;
            }
        }

        batch.ClearItems();
        _buckets.Build(_dCtx, n);

        for (int k = 0; k < _buckets.Count; k++)
        {
            int d   = _buckets.At(k);
            var ctx = _dCtx[d];
            int g   = (int)ctx.CurrentGoal;

            int biasRow  = -1;
            int fleeSlot = FleeSlots[g];
            if (fleeSlot >= 0 && ctx.FleeBias != 0f)
            {
                var row = batch.BiasRow(d);
                row.Clear();
                row[fleeSlot] = ctx.FleeBias;
                biasRow = d;
            }

            var mlp  = ctx.ExecMLPs[g];
            int slot = executors[g].BeginForward(mlp, _dDt[d]);

            batch.Add(new ForwardItem
            {
                Ctx         = mlp.Ptr,
                Lstm        = ctx.Reservoir.Ptr,
                Net         = 1 + g,
                Slot        = slot,
                InputRow    = _dRow[d],
                BiasRow     = biasRow,
                Owner       = d,
                Dt          = mlp.DeltaTime,
                Temperature = mlp.Params.SoftmaxTemperature,
            });
        }

        int execCount = batch.Count;
        batch.Schedule(_kernels, _nets, LstmHidden).Complete();

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
                item.BiasRow >= 0, ExecDelaySteps, execCritics[g], Gamma, simTime,
                GoalMinDuration[g], GoalMaxDuration[g], execEps, decay);

            int action    = ActionSubsets[g][ticket.Predicted];
            float clamped = math.clamp(ticket.Duration,
                ActionDurationTable.Min(action), ActionDurationTable.Max(action));
            ticket.Duration = clamped;

            ctx.ExecWindow.Begin(ticket.DecisionId, simTime, clamped);

            _dDecision[d] = new BrainDecision(action, ticket.DecisionId, ctx.CoordDecisionId,
                ctx.CurrentGoal, simTime, clamped, new float2(ticket.HeadingSin, ticket.HeadingCos));
            _dMade[d] = true;
        }

        RunTraining();
    }

    public void RunTraining()
    {
        float clip = SimulationRules.Frame.PpoClipEpsilon;

        var handle = coordinator.ScheduleTraining(clip);
        for (int g = 0; g < GoalCount; g++)
            handle = JobHandle.CombineDependencies(handle, executors[g].ScheduleTraining(clip));
        handle.Complete();

        coordinator.CompleteTraining();
        for (int g = 0; g < GoalCount; g++)
            executors[g].CompleteTraining();
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

    private static bool HasBias(float[] bias)
    {
        for (int i = 0; i < bias.Length; i++)
            if (bias[i] != 0f) return true;
        return false;
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

        for (int i = 0; i < GoalCount; i++)
            executors[i].FlushTerminal(ctx.ExecMLPs[i], execCritics[i], Gamma,
                                       i == g ? scaled : 0f);

        FlushGoalRewardToCoordinator(ctx);
        coordinator.FlushTerminal(ctx.CoordMLP, coordCritic, Gamma, scaled);

        ctx.CoordDecisionId      = 0;
        ctx.GoalEndTime          = 0f;
        ctx.GoalTotalReward      = 0f;
        ctx.GoalDiscountedReturn = 0f;
        ctx.GoalDiscountFactor   = 1f;
        ctx.GoalRewardCount      = 0;
    }

    private void FlushGoalRewardToCoordinator(EntityBrainContext ctx)
    {
        if (ctx.GoalRewardCount == 0) return;

        var item = ctx.CoordMLP.Decisions.Find(ctx.CoordDecisionId);
        if (item != null)
        {
            float clip = SimulationRules.Active.RewardClipSigma;
            item.Evaluation    = Mathf.Clamp(ctx.GoalDiscountedReturn, -clip, clip);
            item.RewardApplied = true;
        }
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
            int expected = ActionSubsets[i].Length + 1 + HeadingOutputs;
            if (actual != expected)
            {
                Debug.LogError($"Снапшот мозга несовместим: exec {i} имеет {actual} выходов, ожидалось {expected} (directional head)");
                return null;
            }
        }

        return new SharedHierarchicalBrain(reservoir, coordinator, executors);
    }

    public SharedBrainSnapshot ToSnapshot() => new SharedBrainSnapshot(this);

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