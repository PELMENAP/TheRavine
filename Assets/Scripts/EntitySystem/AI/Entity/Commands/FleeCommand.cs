using Unity.Mathematics;

public class FleeCommand : PlannedMoveCommand
{
    private float2 _threat;
    private int    _leg;
    private int    _legs;
    private float  _legLength;

    public FleeCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    protected override EntityCommandStatus OnBegin()
    {
        model.DialogHost.UpdateDialogPosition((IDialogListener)model.Motor);

        var r = SimulationRules.Active;
        float2 self  = model.Position2D;
        float  total = model.Tuning.DetectionRadius * r.FleeDistanceMul;

        if (!model.TryGetThreatSource(out _threat) && !TryDangerSource(r, self, total, out _threat)) return Complete();

        _legs      = math.max(1, r.FleeZigzagLegs);
        _leg       = 0;
        _legLength = _legs > 1 ? math.max(total - r.FleeSprintDistance, 0f) / (_legs - 1) : total;

        float speed = model.Tuning.RunSpeed * (model.IsWarned ? r.WarnedFleeSpeedMul : 1f);
        float first = _legs > 1 ? math.min(r.FleeSprintDistance, total) : total;
        return BeginPlanned(MoveIntent.Flee, self - _threat, default, false,
            first, speed, model.Tuning.EnergyCostRunning, _threat, true, 1f, 0f);
    }

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        if (++_leg >= _legs || _legLength <= SimulationRules.Frame.FleeMinLegLength) return Complete();

        var r = SimulationRules.Active;
        float2 self = model.Position2D;
        float  side = (_leg & 1) == 0 ? 1f : -1f;
        return BeginPlanned(MoveIntent.Flee, self - _threat, default, false,
            _legLength, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving, _threat, true,
            side, r.FleeZigzagCurvature);
    }

    private bool TryDangerSource(SimulationRules r, float2 self, float radius, out float2 source)
    {
        source = default;
        var nest = model.Nest;
        if (nest == null) return false;

        float spread = nest.SampleDirection(self, radius, r.WanderGradientSamples, 0f, 0f, -1f, out float2 toDanger);
        if (spread <= r.WanderGradientMin) return false;
        source = self + toDanger * radius;
        return true;
    }
}
