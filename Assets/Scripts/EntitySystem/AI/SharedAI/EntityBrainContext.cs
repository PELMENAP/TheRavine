public class EntityBrainContext : System.IDisposable
{
    public readonly LSTMContext          Reservoir;
    public readonly PerceptronContext    CoordMLP;
    public readonly float[]              CoordCombined;

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

        Reservoir     = new LSTMContext(inputSize, lstmHidden);
        CoordMLP      = new PerceptronContext(coordLayout, geneParams);
        CoordCombined = new float[combined];

        ExecMLPs     = new PerceptronContext[goalCount];
        ExecCombined = new float[goalCount][];

        for (int i = 0; i < goalCount; i++)
        {
            ExecMLPs[i]     = new PerceptronContext(execLayouts[i], geneParams);
            ExecCombined[i] = new float[combined];
        }

        CoordBias = new float[goalCount];
    }

    public void ResetMemory() => Reservoir.Reset();

    public void Dispose()
    {
        Reservoir.Dispose();
        CoordMLP.Dispose();
        for (int i = 0; i < ExecMLPs.Length; i++)
            ExecMLPs[i].Dispose();
    }
}
