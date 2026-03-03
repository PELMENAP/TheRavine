using System;
using UnityEngine;
using TheRavine.Extensions;
public class HierarchicalBrain
{
    // ─── Высокоуровневые цели ────────────────────────────────────────────────────
    public enum Goal
    {
        Survive = 0,  // выжить: убегать, есть, лечиться
        Hunt    = 1,  // охота: атаковать, преследовать
        Forage  = 2,  // собирательство: запоминать точки, ходить к точкам, есть
        Social  = 3,  // социальное: говорить, размножаться, стоять
    }
    public const int GoalCount = 4;

    private static readonly int[][] ActionSubsets = {
        new[] { 1, 5, 6, 0 },       // Survive:  Wander, Flee, Eat, Idle
        new[] { 4, 1, 0, 5 },       // Hunt:     Attack, Wander, Idle, Flee
        new[] { 2, 3, 6, 1 },       // Forage:   RememberPoint, GoToPoint, Eat, Wander
        new[] { 8, 7, 0, 1 },       // Social:   Speech, Reproduce, Idle, Wander
    };

    private const int CoordinatorDelaySteps = 10; // координатор учится медленно
    private const int ExecutorDelaySteps    = 3;  // исполнители — быстро
    private const int MinGoalDuration       = 4;  // минимум шагов на одну цель
    private const int MaxGoalDuration       = 12; // максимум

    private readonly LSTMPerceptronHybrid              _coordinator;
    private readonly LSTMPerceptronHybrid[]            _executors;

    private Goal _currentGoal      = Goal.Survive;
    private int  _goalStepsLeft    = 0;
    private int  _goalDuration     = MinGoalDuration;
    private float _goalTotalReward = 0f;
    private int  _goalRewardCount  = 0;

    public Goal   CurrentGoal    => _currentGoal;
    public int    GoalStepsLeft  => _goalStepsLeft;
    public HierarchicalBrain(int inputSize, int lstmHidden = 16)
    {
        _coordinator = new LSTMPerceptronHybrid(
            inputSize, lstmHidden,
            h1: 32, h2: 16, h3: 8,
            outputSize: GoalCount,
            delaySteps: CoordinatorDelaySteps);

        _executors = new LSTMPerceptronHybrid[GoalCount];
        for (int i = 0; i < GoalCount; i++)
        {
            _executors[i] = new LSTMPerceptronHybrid(
                inputSize, lstmHidden,
                h1: 32, h2: 16, h3: 8,
                outputSize: ActionSubsets[i].Length,
                delaySteps: ExecutorDelaySteps);
        }
    }

    public HierarchicalBrain(HierarchicalBrain parent)
    {
        _coordinator = new LSTMPerceptronHybrid(parent._coordinator);

        _executors = new LSTMPerceptronHybrid[GoalCount];
        for (int i = 0; i < GoalCount; i++)
            _executors[i] = new LSTMPerceptronHybrid(parent._executors[i]);
    }

    public HierarchicalBrain(HierarchicalBrain parentA, HierarchicalBrain parentB)
    {
        _coordinator = RavineRandom.RangeBool()
            ? new LSTMPerceptronHybrid(parentA._coordinator)
            : new LSTMPerceptronHybrid(parentB._coordinator);

        _executors = new LSTMPerceptronHybrid[GoalCount];
        for (int i = 0; i < GoalCount; i++)
        {
            _executors[i] = RavineRandom.RangeBool()
                ? new LSTMPerceptronHybrid(parentA._executors[i])
                : new LSTMPerceptronHybrid(parentB._executors[i]);
        }
    }
    public int Predict(
        float[] input,
        float coordinatorEpsilon = 0.05f,
        float executorEpsilon    = 0.15f)
    {
        if (_goalStepsLeft <= 0)
        {
            FlushGoalRewardToCoordinator();

            int goalIndex = _coordinator.Predict(input, coordinatorEpsilon);
            _currentGoal   = (Goal)goalIndex;
            _goalDuration  = RavineRandom.RangeInt(MinGoalDuration, MaxGoalDuration + 1);
            _goalStepsLeft = _goalDuration;

            _goalTotalReward = 0f;
            _goalRewardCount = 0;
        }

        _goalStepsLeft--;

        int goalIdx       = (int)_currentGoal;
        int localAction   = _executors[goalIdx].Predict(input, executorEpsilon);
        int entityAction  = ActionSubsets[goalIdx][localAction];

        return entityAction;
    }
    public void GiveReward(float reward)
    {
        int goalIdx = (int)_currentGoal;

        var executorList = _executors[goalIdx].DelayedList;
        if (executorList.Count > 0)
            executorList[executorList.Count - 1].Evaluation = Mathf.Clamp01(reward);

        _goalTotalReward += reward;
        _goalRewardCount++;
    }

    public void ResetMemory()
    {
        _coordinator.ResetMemory();
        foreach (var executor in _executors)
            executor.ResetMemory();
    }

    public float GetCoordinatorEntropy() => _coordinator.AverageEntropy;

    public float GetExecutorEntropy(Goal goal) =>
        _executors[(int)goal].AverageEntropy;

    public GeneticParameters GetCoordinatorParams() =>
        _coordinator.GetGeneticParameters();

    public GeneticParameters GetExecutorParams(Goal goal) =>
        _executors[(int)goal].GetGeneticParameters();

    private void FlushGoalRewardToCoordinator()
    {
        if (_goalRewardCount == 0) return;

        float avgReward  = _goalTotalReward / _goalRewardCount;
        var coordinatorList = _coordinator.DelayedList;

        if (coordinatorList.Count > 0)
            coordinatorList[coordinatorList.Count - 1].Evaluation =
                Mathf.Clamp01(avgReward);
    }
}