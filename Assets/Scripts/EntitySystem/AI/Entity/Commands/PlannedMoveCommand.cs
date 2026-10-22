using Unity.Mathematics;

public abstract class PlannedMoveCommand : EntityCommand
{
    private double _pauseEnd;
    private bool   _pausing;

    protected PlannedMoveCommand(EntityModel model) : base(model) { }

    protected virtual bool PauseAfterArrival => true;

    protected EntityCommandStatus BeginPlanned(MoveIntent intent, float2 direction, float2 target, bool hasTarget,
        float radius, float speed, float energyCost, float2 threat = default, bool hasThreat = false,
        float side = 0f, float curvature = float.NaN)
    {
        _pausing = false;
        float remaining = math.max(0f, decision.EndTime - SimulationClock.Time);
        float travel    = radius / math.max(speed * model.SpeedMul, SimulationRules.Frame.TimingMinSpeed) * SimulationRules.Frame.LegTimeSlack;
        if (travel > remaining)
        {
            remaining = travel;
            ExtendWatchdog(SimulationClock.TimeD + travel);
        }
        StartPlannedMove(intent, direction, target, hasTarget, radius, speed, remaining, energyCost,
            threat, hasThreat, side, curvature);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (_pausing) return Elapsed(_pauseEnd) ? Complete() : EntityCommandStatus.Running;

        if (!TryFinishMove(out var move, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();
        if (TryReplanBlocked(in move)) return EntityCommandStatus.Running;

        var status = OnArrived(in move);
        if (status != EntityCommandStatus.Completed || !PauseAfterArrival) return status;

        _pauseEnd = PauseUntil();
        _pausing  = true;
        return Elapsed(_pauseEnd) ? Complete() : EntityCommandStatus.Running;
    }

    protected virtual EntityCommandStatus OnArrived(in MoveResult move) => Complete();

    protected override void OnCancel() => _pausing = false;
}
