using System;

public class EntityBrainContext
{
    public readonly LSTMContext        CoordLSTM;
    public readonly PerceptronContext  CoordMLP;
    public readonly float[]            CoordCombined;

    public readonly LSTMContext[]        ExecLSTMs;
    public readonly PerceptronContext[]  ExecMLPs;
    public readonly float[][]            ExecCombined;

    public SharedHierarchicalBrain.Goal CurrentGoal = SharedHierarchicalBrain.Goal.Survive;
    public int   GoalStepsLeft;
    public float GoalTotalReward;
    public int   GoalRewardCount;

    public EntityBrainContext(
        int inputSize,
        int lstmHidden,
        int[] coordLayerSizes,
        int[][] execLayerSizes,
        GeneticParameters geneParams)
    {
        int goalCount = SharedHierarchicalBrain.GoalCount;
        int combined  = inputSize + lstmHidden;

        CoordLSTM     = new LSTMContext(inputSize, lstmHidden);
        CoordMLP      = new PerceptronContext(coordLayerSizes, geneParams);
        CoordCombined = new float[combined];

        ExecLSTMs    = new LSTMContext[goalCount];
        ExecMLPs     = new PerceptronContext[goalCount];
        ExecCombined = new float[goalCount][];

        for (int i = 0; i < goalCount; i++)
        {
            ExecLSTMs[i]    = new LSTMContext(inputSize, lstmHidden);
            ExecMLPs[i]     = new PerceptronContext(execLayerSizes[i], geneParams);
            ExecCombined[i] = new float[combined];
        }
    }

    public void ResetMemory()
    {
        CoordLSTM.Reset();
        foreach (var l in ExecLSTMs) l.Reset();
    }
}