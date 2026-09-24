public class EntityBrainContext : System.IDisposable
{
    public readonly LSTMContext          Reservoir;
    public readonly PerceptronContext    CoordMLP;
    public readonly float[]              CoordCombined;

    public readonly PerceptronContext[]  ExecMLPs;
    public readonly float[][]            ExecCombined;

    public SharedHierarchicalBrain.Goal CurrentGoal = SharedHierarchicalBrain.Goal.Survive;
    public PlanKind CurrentPlan = PlanKind.Count;
    public PlanHint PlanHints;
    public int   ExecMask;
    public bool  ExecForced;
    public bool  SkipExec;
    public float GoalBonus;
    public float ColonyStorage;
    public DecisionWindow ExecWindow;
    public float GoalEndTime;
    public int   CoordDecisionId;
    public float GoalTotalReward;
    public int   GoalRewardCount;

    public float GoalDiscountedReturn;
    public float GoalStartTime;
    public float GoalStartEnergy;
    public int   GoalFoodEaten;
    public int   GoalRestCount;
    public float GoalNovelty;
    public float EnergyNorm;
    public float IntrinsicReward;
    public readonly float[] CoordBias;

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

        CoordBias = new float[SharedHierarchicalBrain.PlanCount];
    }

    public void ResetMemory() => Reservoir.Reset();

    public void BeginGoal(float time)
    {
        GoalStartTime        = time;
        GoalStartEnergy      = EnergyNorm;
        GoalNovelty          = IntrinsicReward;
        GoalFoodEaten        = 0;
        GoalRestCount        = 0;
        GoalBonus            = 0f;
        GoalTotalReward      = 0f;
        GoalDiscountedReturn = 0f;
        GoalRewardCount      = 0;
    }

    public void Dispose()
    {
        Reservoir.Dispose();
        CoordMLP.Dispose();
        for (int i = 0; i < ExecMLPs.Length; i++)
            ExecMLPs[i].Dispose();
    }
}
