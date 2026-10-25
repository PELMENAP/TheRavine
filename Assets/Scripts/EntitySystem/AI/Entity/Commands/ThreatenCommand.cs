using Unity.Mathematics;

using TheRavine.Extensions;

public class ThreatenCommand : EntityCommand
{
    private double     _holdEnd;
    private HuntTarget _target;
    private float2     _facing;

    public ThreatenCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var target = model.CachedHuntTarget;
        if (!target.IsValid) return Fail();
        float dist = math.distance(model.Position2D, target.Position2D);
        if (dist > model.Tuning.AttackRange * r.ThreatenRangeMul) return Fail();

        var stats = model.Stats;
        stats.En = math.max(0f, stats.En - r.ThreatenEnergyCost);
        _target  = target;
        _facing  = float2.zero;
        _holdEnd = HoldUntil(r.ThreatenDuration);
        Face(r);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (Elapsed(_holdEnd)) return Complete();
        if (!_target.IsValid) return Complete();
        if (!model.Motor.IsMoving) Face(SimulationRules.Active);
        return EntityCommandStatus.Running;
    }

    private void Face(SimulationRules r)
    {
        float2 self = model.Position2D;
        float2 to   = math.normalizesafe(_target.Position2D - self);
        if (math.lengthsq(to) < 1e-6f) return;

        bool first = math.lengthsq(_facing) < 1e-6f;
        if (!first && math.dot(to, _facing) >= math.cos(r.ThreatenReorientAngle)) return;

        _facing = to;
        float2 step = self + to * r.ThreatenStepDistance;
        StartMove(Extension.ToWorld(in step, model.Motor.Position().y), model.Tuning.MoveSpeed,
            (float)math.max(_holdEnd - SimulationClock.TimeD, r.ThreatenMinHoldSeconds), model.Tuning.EnergyCostMoving);
    }

    protected override void OnCancel() => _target = default;
}
