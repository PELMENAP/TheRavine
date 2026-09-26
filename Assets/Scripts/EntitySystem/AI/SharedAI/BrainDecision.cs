using Unity.Mathematics;


public enum EntityCommandStatus { Started, Running, Completed, Interrupted, Failed }

public readonly struct BrainDecision
{
    public readonly int   Action;
    public readonly int   ExecDecisionId;
    public readonly int   CoordDecisionId;
    public readonly SharedHierarchicalBrain.Goal Goal;
    public readonly float StartTime;
    public readonly float Duration;
    public readonly float2 Heading;
    public readonly float  Curvature;
    public readonly float4 Speech;
    public readonly PlanKind Plan;
    public readonly float  PlanEnd;
    public readonly byte   Direction;
    public readonly float  DirOffset;
    public readonly byte   Signal;

    public BrainDecision(int action, int execDecisionId, int coordDecisionId,
        SharedHierarchicalBrain.Goal goal, float startTime, float duration, in float2 heading,
        float curvature = 0f, float4 speech = default, PlanKind plan = PlanKind.Count, float planEnd = 0f,
        byte direction = (byte)DirectionKind.None, float dirOffset = 0f, byte signal = SpeechSignal.None)
    {
        Direction = direction;
        DirOffset = dirOffset;
        Signal = signal;
        Plan = plan;
        PlanEnd = planEnd;
        Curvature = curvature;
        Speech = speech;
        Action = action;
        ExecDecisionId = execDecisionId;
        CoordDecisionId = coordDecisionId;
        Goal = goal;
        StartTime = startTime;
        Duration = duration;
        Heading = heading;
    }

    public float EndTime    => StartTime + Duration;
    public bool  IsValid    => ExecDecisionId != 0;
    public bool  HasHeading => math.lengthsq(Heading) > 1e-6f;
    public bool  HasPlan    => Plan < PlanKind.Count;
    public bool  HasDirection => Direction < (byte)DirectionKind.Count;

    public BrainDecision WithStep(int action, float startTime, float duration, bool keepHeading)
    {
        float2 heading = keepHeading ? Heading : float2.zero;
        return new(action, ExecDecisionId, CoordDecisionId, Goal, startTime, duration, in heading,
                   Curvature, Speech, Plan, PlanEnd, Direction, DirOffset, Signal);
    }

    public BrainDecision WithDirection(byte direction)
        => new(Action, ExecDecisionId, CoordDecisionId, Goal, StartTime, Duration, in Heading,
               Curvature, Speech, Plan, PlanEnd, direction, DirOffset, Signal);
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