public class EntityBrainContext
{
    public readonly LSTMContext        CoordLSTM;
    public readonly PerceptronContext  CoordMLP;
    public readonly float[]            CoordCombined;

    public readonly LSTMContext[]        ExecLSTMs;
    public readonly PerceptronContext[]  ExecMLPs;
    public readonly float[][]            ExecCombined;

    public SharedHierarchicalBrain.Goal CurrentGoal = SharedHierarchicalBrain.Goal.Survive;
    public DecisionWindow ExecWindow;
    public float GoalEndTime;
    public int   CoordDecisionId;
    public float GoalTotalReward;
    public int   GoalRewardCount;

    public float GoalDiscountedReturn;
    public float GoalDiscountFactor;
    public float IntrinsicReward;
    public readonly float[] CoordBias;
    public readonly float[][] ExecBias;
    public float FleeBias;

    public EntityBrainContext(
        int inputSize,
        int lstmHidden,
        PerceptronLayout coordLayout,
        PerceptronLayout[] execLayouts,
        GeneticParameters geneParams)
    {
        int goalCount = SharedHierarchicalBrain.GoalCount;
        int combined  = inputSize + lstmHidden;

        CoordLSTM     = new LSTMContext(inputSize, lstmHidden);
        CoordMLP      = new PerceptronContext(coordLayout, geneParams);
        CoordCombined = new float[combined];

        ExecLSTMs    = new LSTMContext[goalCount];
        ExecMLPs     = new PerceptronContext[goalCount];
        ExecCombined = new float[goalCount][];

        for (int i = 0; i < goalCount; i++)
        {
            ExecLSTMs[i]    = new LSTMContext(inputSize, lstmHidden);
            ExecMLPs[i]     = new PerceptronContext(execLayouts[i], geneParams);
            ExecCombined[i] = new float[combined];
        }

        CoordBias = new float[goalCount];
        ExecBias  = new float[goalCount][];
        for (int i = 0; i < goalCount; i++)
            ExecBias[i] = new float[SharedHierarchicalBrain.ActionSubsets[i].Length];
    }

    public void ResetMemory()
    {
        CoordLSTM.Reset();
        foreach (var l in ExecLSTMs) l.Reset();
    }
}