using System;
using UnityEngine;

public class SharedHierarchicalBrain
{
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

    private static int[] BuildExecSizes(int combined, int goal)
        => new[] { combined, 64, 32, 32, ActionSubsets[goal].Length + 1 };


    private readonly LSTMMemory         coordLSTM;
    private readonly DelayedPerceptron  coordinator;
    private readonly LSTMMemory[]       execLSTMs;
    private readonly DelayedPerceptron[] executors;

    internal LSTMMemory          CoordLSTM   => coordLSTM;
    internal DelayedPerceptron   Coordinator => coordinator;
    internal LSTMMemory[]        ExecLSTMs   => execLSTMs;
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

    public SharedHierarchicalBrain(int inputSize, int lstmHidden = 32)
    {
        InputSize  = inputSize;
        LstmHidden = lstmHidden;
        int combined = inputSize + lstmHidden;

        CoordLayerSizes = BuildCoordSizes(combined);
        ExecLayerSizes  = new int[GoalCount][];
        for (int i = 0; i < GoalCount; i++)
            ExecLayerSizes[i] = BuildExecSizes(combined, i);

        coordLSTM   = new LSTMMemory(inputSize, lstmHidden);
        coordinator = new DelayedPerceptron(CoordLayerSizes);

        execLSTMs = new LSTMMemory[GoalCount];
        executors = new DelayedPerceptron[GoalCount];
        for (int i = 0; i < GoalCount; i++)
        {
            execLSTMs[i] = new LSTMMemory(inputSize, lstmHidden);
            executors[i] = new DelayedPerceptron(ExecLayerSizes[i]);
        }

        coordCritic = new ValueCritic(combined);
        execCritics = new ValueCritic[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            execCritics[i] = new ValueCritic(combined);

        _rnd = new RandomNetworkDistillation(inputSize);
    }

    public SharedHierarchicalBrain(SharedHierarchicalBrain src) : this(src.InputSize, src.LstmHidden)
    {
        coordLSTM   = new LSTMMemory(src.coordLSTM);
        coordinator = new DelayedPerceptron(src.coordinator);
        for (int i = 0; i < GoalCount; i++)
        {
            execLSTMs[i] = new LSTMMemory(src.execLSTMs[i]);
            executors[i] = new DelayedPerceptron(src.executors[i]);
        }
    }

    public EntityBrainContext CreateContext(GeneticParameters? p = null)
        => new EntityBrainContext(InputSize, LstmHidden, CoordLayerSizes, ExecLayerSizes,
                                   p ?? GeneticParameters.Default);

    public bool TryDecide(float[] input, EntityBrainContext ctx, float simTime, float dt,
        out BrainDecision decision, float coordEps = 0.05f, float execEps = 0.15f)
    {
        decision = default;
        if (ctx.ExecWindow.IsRunning(simTime)) return false;

        ctx.IntrinsicReward = _rnd.ComputeIntrinsicReward(input);

        if (simTime >= ctx.GoalEndTime)
        {
            FlushGoalRewardToCoordinator(ctx);

            float[] coordH = coordLSTM.Step(input, ctx.CoordLSTM);
            BuildCombined(input, coordH, ctx.CoordCombined);

            var goalTicket = coordinator.Decide(ctx.CoordCombined, ctx.CoordMLP, CoordDelaySteps,
                coordCritic, Gamma, dt, simTime,
                ActionDurationTable.MinGoalSeconds, ActionDurationTable.MaxGoalSeconds, coordEps);

            ctx.CurrentGoal          = (Goal)goalTicket.Predicted;
            ctx.CoordDecisionId      = goalTicket.DecisionId;
            ctx.GoalEndTime          = simTime + goalTicket.Duration;
            ctx.GoalDiscountedReturn = 0f;
            ctx.GoalDiscountFactor   = 1f;
            ctx.GoalRewardCount      = 0;
        }

        int g     = (int)ctx.CurrentGoal;
        float[] h = execLSTMs[g].Step(input, ctx.ExecLSTMs[g]);
        BuildCombined(input, h, ctx.ExecCombined[g]);

        var subset = ActionSubsets[g];
        float minD = float.MaxValue, maxD = 0f;
        for (int i = 0; i < subset.Length; i++)
        {
            float lo = ActionDurationTable.Min(subset[i]);
            float hi = ActionDurationTable.Max(subset[i]);
            if (lo < minD) minD = lo;
            if (hi > maxD) maxD = hi;
        }

        var ticket = executors[g].Decide(ctx.ExecCombined[g], ctx.ExecMLPs[g], ExecDelaySteps,
            execCritics[g], Gamma, dt, simTime, minD, maxD, execEps);

        int action = subset[ticket.Predicted];
        float clamped = Mathf.Clamp(ticket.Duration,
            ActionDurationTable.Min(action), ActionDurationTable.Max(action));
        ticket.Duration = clamped;

        ctx.ExecWindow.Begin(ticket.DecisionId, simTime, clamped);

        decision = new BrainDecision(action, ticket.DecisionId, ctx.CoordDecisionId,
            ctx.CurrentGoal, simTime, clamped);
        return true;
    }

    public void GiveReward(float reward, int decisionId, EntityBrainContext ctx)
    {
        float shaped = Mathf.Clamp(reward + ctx.IntrinsicReward * CuriosityWeight, -1f, 1.2f);

        int g = (int)ctx.CurrentGoal;
        var mlp = ctx.ExecMLPs[g];
        var item = mlp.Decisions.Find(decisionId);
        if (item == null) return;

        item.Evaluation   = shaped;
        item.RewardApplied = true;
        mlp.Diagnostics.RecordRewardLatency(SimulationClock.Time - item.StartTime);

        ctx.GoalDiscountedReturn += shaped * ctx.GoalDiscountFactor;
        ctx.GoalDiscountFactor   *= Gamma;
        ctx.GoalRewardCount++;
    }

    public void CompleteDecision(int decisionId, float reward, EntityBrainContext ctx,
        float simTime, EntityCommandStatus status)
    {
        GiveReward(reward, decisionId, ctx);

        int g = (int)ctx.CurrentGoal;
        var item = ctx.ExecMLPs[g].Decisions.Find(decisionId);
        float elapsed = item != null ? simTime - item.StartTime : 0f;
        ctx.ExecMLPs[g].Diagnostics.RecordCompletion(
            elapsed, status == EntityCommandStatus.Interrupted);

        if (ctx.ExecWindow.DecisionId == decisionId) ctx.ExecWindow.End();
    }

    private void FlushGoalRewardToCoordinator(EntityBrainContext ctx)
    {
        if (ctx.GoalRewardCount == 0) return;

        var item = ctx.CoordMLP.Decisions.Find(ctx.CoordDecisionId);
        if (item != null)
        {
            item.Evaluation    = Mathf.Clamp(ctx.GoalDiscountedReturn, -1f, 1f);
            item.RewardApplied = true;
        }
    }

    public float GetCoordinatorEntropy(EntityBrainContext ctx) => ctx.CoordMLP.AverageEntropy;
    public Goal  GetCurrentGoal(EntityBrainContext ctx)        => ctx.CurrentGoal;

    public float GetExecutorEntropy(Goal goal, EntityBrainContext ctx)
        => ctx.ExecMLPs[(int)goal].AverageEntropy;

    private static void BuildCombined(float[] input, float[] lstmH, float[] combined)
    {
        Array.Copy(input, 0, combined, 0,            input.Length);
        Array.Copy(lstmH, 0, combined, input.Length, lstmH.Length);
    }

    private SharedHierarchicalBrain(
        LSTMMemory coordLSTM, DelayedPerceptron coordinator,
        LSTMMemory[] execLSTMs, DelayedPerceptron[] executors)
    {
        InputSize  = coordLSTM.InputSize;
        LstmHidden = coordLSTM.HiddenSize;

        this.coordLSTM   = coordLSTM;
        this.coordinator = coordinator;
        this.execLSTMs   = execLSTMs;
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
    }

    internal static SharedHierarchicalBrain FromModels(
        LSTMMemory coordLSTM, DelayedPerceptron coordinator,
        LSTMMemory[] execLSTMs, DelayedPerceptron[] executors)
    {
        if (coordLSTM == null || coordinator == null ||
            execLSTMs == null || executors == null ||
            execLSTMs.Length != GoalCount || executors.Length != GoalCount)
        {
            Debug.LogError("Снапшот мозга некорректен: состав моделей не совпадает");
            return null;
        }

        for (int i = 0; i < GoalCount; i++)
        {
            if (execLSTMs[i] == null || executors[i] == null ||
                execLSTMs[i].InputSize != coordLSTM.InputSize ||
                execLSTMs[i].HiddenSize != coordLSTM.HiddenSize)
            {
                Debug.LogError($"Снапшот мозга некорректен: размеры exec-модели {i} не совпадают с координатором");
                return null;
            }
        }

        return new SharedHierarchicalBrain(coordLSTM, coordinator, execLSTMs, executors);
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