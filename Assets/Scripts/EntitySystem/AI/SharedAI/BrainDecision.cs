using Unity.Mathematics;


public enum EntityCommandStatus { Started, Running, Completed, Interrupted, Failed }

public enum AttackStyle : byte { Pursue = 0, Stalk = 1, Ambush = 2, Count }

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
    public readonly ushort Tokens;
    public readonly PlanKind Plan;
    public readonly float  PlanEnd;
    public readonly byte   Direction;
    public readonly float  DirOffset;
    public readonly byte   SpeechLen;
    public readonly float2 Destination;
    public readonly bool   HasDestination;
    public readonly AttackStyle Style;
    public readonly float  Tempo;

    public BrainDecision(int action, int execDecisionId, int coordDecisionId,
        SharedHierarchicalBrain.Goal goal, float startTime, float duration, in float2 heading,
        float curvature = 0f, ushort tokens = 0, PlanKind plan = PlanKind.Count, float planEnd = 0f,
        byte direction = (byte)DirectionKind.None, float dirOffset = 0f, byte speechLen = 0,
        float2 destination = default, bool hasDestination = false,
        AttackStyle style = AttackStyle.Pursue, float tempo = 0f)
    {
        Style = style;
        Tempo = tempo;
        Destination = destination;
        HasDestination = hasDestination;
        Direction = direction;
        DirOffset = dirOffset;
        SpeechLen = speechLen;
        Plan = plan;
        PlanEnd = planEnd;
        Curvature = curvature;
        Tokens = tokens;
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
                   Curvature, Tokens, Plan, PlanEnd, Direction, DirOffset, SpeechLen, Destination, HasDestination, Style, Tempo);
    }

    public BrainDecision WithDirection(byte direction)
        => new(Action, ExecDecisionId, CoordDecisionId, Goal, StartTime, Duration, in Heading,
               Curvature, Tokens, Plan, PlanEnd, direction, DirOffset, SpeechLen, Destination, HasDestination, Style, Tempo);

    public BrainDecision WithDestination(float2 destination)
        => new(Action, ExecDecisionId, CoordDecisionId, Goal, StartTime, Duration, in Heading,
               Curvature, Tokens, Plan, PlanEnd, Direction, DirOffset, SpeechLen, destination, true, Style, Tempo);
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