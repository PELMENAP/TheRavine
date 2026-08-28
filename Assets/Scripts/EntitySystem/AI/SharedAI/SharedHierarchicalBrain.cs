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
    private const int MinGoalDuration  = 2;
    private const int MaxGoalDuration  = 10;

    private readonly LSTMMemory         coordLSTM;
    private readonly DelayedPerceptron  coordinator;
    private readonly LSTMMemory[]       execLSTMs;
    private readonly DelayedPerceptron[] executors;

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

        CoordLayerSizes = new[] { combined, 64, 32, 32, GoalCount };
        ExecLayerSizes  = new int[GoalCount][];
        for (int i = 0; i < GoalCount; i++)
            ExecLayerSizes[i] = new[] { combined, 64, 32, 32, ActionSubsets[i].Length };

        coordLSTM   = new LSTMMemory(inputSize, lstmHidden);
        coordinator = new DelayedPerceptron(combined, 64, 32, 32, GoalCount);

        execLSTMs = new LSTMMemory[GoalCount];
        executors = new DelayedPerceptron[GoalCount];
        for (int i = 0; i < GoalCount; i++)
        {
            execLSTMs[i] = new LSTMMemory(inputSize, lstmHidden);
            executors[i] = new DelayedPerceptron(combined, 64, 32, 32, ActionSubsets[i].Length);
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

    public int Predict(float[] input, EntityBrainContext ctx,
        float coordEps = 0.05f, float execEps = 0.15f)
    {
        ctx.IntrinsicReward = _rnd.ComputeIntrinsicReward(input);
        
        if (ctx.GoalStepsLeft <= 0)
        {
            FlushGoalRewardToCoordinator(ctx);

            float[] coordH = coordLSTM.Step(input, ctx.CoordLSTM);
            BuildCombined(input, coordH, ctx.CoordCombined);

            int goalIdx = coordinator.Predict(ctx.CoordCombined, ctx.CoordMLP, CoordDelaySteps, coordCritic, Gamma, coordEps);

            ctx.CurrentGoal     = (Goal)goalIdx;
            ctx.GoalStepsLeft   = RavineRandom.RangeInt(MinGoalDuration, MaxGoalDuration + 1);
            ctx.GoalDiscountedReturn = 0f;
            ctx.GoalDiscountFactor   = 1f;
            ctx.GoalRewardCount      = 0;
        }

        ctx.GoalStepsLeft--;

        int g       = (int)ctx.CurrentGoal;
        float[] h   = execLSTMs[g].Step(input, ctx.ExecLSTMs[g]);
        BuildCombined(input, h, ctx.ExecCombined[g]);

        int localAction = executors[g].Predict(ctx.ExecCombined[g], ctx.ExecMLPs[g], ExecDelaySteps, execCritics[g], Gamma, execEps);
        return ActionSubsets[g][localAction];
    }

    public void GiveReward(float reward, EntityBrainContext ctx)
    {
        float shaped = Mathf.Clamp(reward + ctx.IntrinsicReward * CuriosityWeight, -1f, 1.2f);

        int g = (int)ctx.CurrentGoal;
        var list = ctx.ExecMLPs[g].DelayedList;
        if (list.Count > 0)
            list[list.Count - 1].Evaluation = shaped;

        ctx.GoalDiscountedReturn += shaped * ctx.GoalDiscountFactor;
        ctx.GoalDiscountFactor *= Gamma;
        ctx.GoalRewardCount++;
    }

    public float GetCoordinatorEntropy(EntityBrainContext ctx) => ctx.CoordMLP.AverageEntropy;
    public Goal  GetCurrentGoal(EntityBrainContext ctx)        => ctx.CurrentGoal;

    public float GetExecutorEntropy(Goal goal, EntityBrainContext ctx)
        => ctx.ExecMLPs[(int)goal].AverageEntropy;

    private void FlushGoalRewardToCoordinator(EntityBrainContext ctx)
    {
        if (ctx.GoalRewardCount == 0) return;

        var list = ctx.CoordMLP.DelayedList;
        if (list.Count > 0)
            list[list.Count - 1].Evaluation = Mathf.Clamp(ctx.GoalDiscountedReturn, -1f, 1f);
    }
    private static void BuildCombined(float[] input, float[] lstmH, float[] combined)
    {
        Array.Copy(input, 0, combined, 0,            input.Length);
        Array.Copy(lstmH, 0, combined, input.Length, lstmH.Length);
    }
}