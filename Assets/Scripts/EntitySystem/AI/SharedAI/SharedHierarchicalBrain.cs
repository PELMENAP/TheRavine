using System;
using UnityEngine;
using TheRavine.Extensions;

public class SharedHierarchicalBrain
{
    public enum Goal { Survive = 0, Hunt = 1, Forage = 2, Social = 3 }
    public const int GoalCount = 4;

    public static readonly int[][] ActionSubsets =
    {
        new[] { 1, 5, 6, 0 },   // Survive: Wander, Flee, Eat, Idle
        new[] { 4, 1, 0, 5 },   // Hunt:    Attack, Wander, Idle, Flee
        new[] { 2, 3, 6, 1 },   // Forage:  RememberPoint, GoToPoint, Eat, Wander
        new[] { 8, 7, 0, 1 },   // Social:  Speech, Reproduce, Idle, Wander
    };

    private const int CoordDelaySteps  = 10;
    private const int ExecDelaySteps   = 3;
    private const int MinGoalDuration  = 2;
    private const int MaxGoalDuration  = 10;

    private readonly LSTMMemory         _coordLSTM;
    private readonly DelayedPerceptron  _coordinator;
    private readonly LSTMMemory[]       _execLSTMs;
    private readonly DelayedPerceptron[] _executors;

    public readonly int   InputSize;
    public readonly int   LstmHidden;
    public readonly int[] CoordLayerSizes;
    public readonly int[][] ExecLayerSizes;

    public SharedHierarchicalBrain(int inputSize, int lstmHidden = 32)
    {
        InputSize  = inputSize;
        LstmHidden = lstmHidden;
        int combined = inputSize + lstmHidden;

        CoordLayerSizes = new[] { combined, 64, 32, 32, GoalCount };
        ExecLayerSizes  = new int[GoalCount][];
        for (int i = 0; i < GoalCount; i++)
            ExecLayerSizes[i] = new[] { combined, 64, 32, 32, ActionSubsets[i].Length };

        _coordLSTM   = new LSTMMemory(inputSize, lstmHidden);
        _coordinator = new DelayedPerceptron(combined, 64, 32, 32, GoalCount);

        _execLSTMs = new LSTMMemory[GoalCount];
        _executors = new DelayedPerceptron[GoalCount];
        for (int i = 0; i < GoalCount; i++)
        {
            _execLSTMs[i] = new LSTMMemory(inputSize, lstmHidden);
            _executors[i] = new DelayedPerceptron(combined, 64, 32, 32, ActionSubsets[i].Length);
        }
    }

    public SharedHierarchicalBrain(SharedHierarchicalBrain src) : this(src.InputSize, src.LstmHidden)
    {
        _coordLSTM   = new LSTMMemory(src._coordLSTM);
        _coordinator = new DelayedPerceptron(src._coordinator);
        for (int i = 0; i < GoalCount; i++)
        {
            _execLSTMs[i] = new LSTMMemory(src._execLSTMs[i]);
            _executors[i] = new DelayedPerceptron(src._executors[i]);
        }
    }

    public EntityBrainContext CreateContext(GeneticParameters? p = null)
        => new EntityBrainContext(InputSize, LstmHidden, CoordLayerSizes, ExecLayerSizes,
                                   p ?? GeneticParameters.Default);

    public int Predict(float[] input, EntityBrainContext ctx,
        float coordEps = 0.05f, float execEps = 0.15f)
    {
        if (ctx.GoalStepsLeft <= 0)
        {
            FlushGoalRewardToCoordinator(ctx);

            float[] coordH = _coordLSTM.Step(input, ctx.CoordLSTM);
            BuildCombined(input, coordH, ctx.CoordCombined);

            int goalIdx = _coordinator.Predict(ctx.CoordCombined, ctx.CoordMLP,
                                                CoordDelaySteps, coordEps);

            ctx.CurrentGoal     = (Goal)goalIdx;
            ctx.GoalStepsLeft   = RavineRandom.RangeInt(MinGoalDuration, MaxGoalDuration + 1);
            ctx.GoalTotalReward = 0f;
            ctx.GoalRewardCount = 0;
        }

        ctx.GoalStepsLeft--;

        int g       = (int)ctx.CurrentGoal;
        float[] h   = _execLSTMs[g].Step(input, ctx.ExecLSTMs[g]);
        BuildCombined(input, h, ctx.ExecCombined[g]);

        int localAction  = _executors[g].Predict(ctx.ExecCombined[g], ctx.ExecMLPs[g],
                                                   ExecDelaySteps, execEps);
        return ActionSubsets[g][localAction];
    }

    public void GiveReward(float reward, EntityBrainContext ctx)
    {
        int g    = (int)ctx.CurrentGoal;
        var list = ctx.ExecMLPs[g].DelayedList;
        if (list.Count > 0)
            list[list.Count - 1].Evaluation = Mathf.Clamp01(reward);

        ctx.GoalTotalReward += reward;
        ctx.GoalRewardCount++;
    }

    public float GetCoordinatorEntropy(EntityBrainContext ctx) => ctx.CoordMLP.AverageEntropy;
    public Goal  GetCurrentGoal(EntityBrainContext ctx)        => ctx.CurrentGoal;

    public float GetExecutorEntropy(Goal goal, EntityBrainContext ctx)
        => ctx.ExecMLPs[(int)goal].AverageEntropy;

    private void FlushGoalRewardToCoordinator(EntityBrainContext ctx)
    {
        if (ctx.GoalRewardCount == 0) return;

        float avg  = ctx.GoalTotalReward / ctx.GoalRewardCount;
        var   list = ctx.CoordMLP.DelayedList;
        if (list.Count > 0)
            list[list.Count - 1].Evaluation = Mathf.Clamp01(avg);
    }

    private static void BuildCombined(float[] input, float[] lstmH, float[] combined)
    {
        Array.Copy(input, 0, combined, 0,            input.Length);
        Array.Copy(lstmH, 0, combined, input.Length, lstmH.Length);
    }
}