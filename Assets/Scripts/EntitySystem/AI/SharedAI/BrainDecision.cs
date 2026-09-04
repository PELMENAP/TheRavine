public enum EntityCommandStatus { Started, Running, Completed, Interrupted, Failed }

public readonly struct BrainDecision
{
    public readonly int   Action;
    public readonly int   ExecDecisionId;
    public readonly int   CoordDecisionId;
    public readonly SharedHierarchicalBrain.Goal Goal;
    public readonly float StartTime;
    public readonly float Duration;

    public BrainDecision(int action, int execDecisionId, int coordDecisionId,
        SharedHierarchicalBrain.Goal goal, float startTime, float duration)
    {
        Action = action;
        ExecDecisionId = execDecisionId;
        CoordDecisionId = coordDecisionId;
        Goal = goal;
        StartTime = startTime;
        Duration = duration;
    }

    public float EndTime => StartTime + Duration;
    public bool  IsValid  => ExecDecisionId != 0;
}

public struct DecisionWindow
{
    public int   DecisionId;
    public float StartTime;
    public float EndTime;
    public bool  Active;

    public void Begin(int decisionId, float now, float duration)
    {
        DecisionId = decisionId;
        StartTime = now;
        EndTime = now + duration;
        Active = true;
    }

    public void End() => Active = false;

    public bool IsRunning(float now) => Active && now < EndTime;

    public bool IsExpired(float now) => !Active || now >= EndTime;
}