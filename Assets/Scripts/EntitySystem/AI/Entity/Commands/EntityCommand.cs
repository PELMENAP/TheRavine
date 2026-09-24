using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;

public interface IEntityCommand
{
    float Reward { get; }
    bool CanExecute();
    CommandSource Source { get; }
    EntityCommandStatus Begin(in BrainDecision decision, CommandSource source);
    EntityCommandStatus Tick(float dt);
    void Cancel();
}

public abstract class EntityCommand : IEntityCommand
{
    protected readonly EntityModel model;
    protected BrainDecision decision;

    private double _watchdog;
    private bool   _moveCut;
    private bool   _moveStarted;

    private PlanRequest _plan;
    private float  _planSpeed;
    private float  _planCost;
    private double _planDeadline;
    private int    _replans;

    public float Reward { get; private set; }
    public CommandSource Source { get; private set; }

    protected EntityCommand(EntityModel m) => model = m;

    public virtual bool CanExecute() => true;
    protected virtual float InterruptionReward => SimulationRules.Active.InterruptionReward;
    protected virtual float FailureReward      => SimulationRules.Active.FailureReward;

    public EntityCommandStatus Begin(in BrainDecision d, CommandSource source)
    {
        decision     = d;
        Source       = source;
        _watchdog    = d.EndTime + SimulationRules.Active.CommandWatchdogGrace;
        _moveStarted = false;
        _moveCut     = false;
        _replans     = 0;
        Reward       = 0f;
        return OnBegin();
    }

    public EntityCommandStatus Tick(float dt)
    {
        var status = OnTick(dt);
        if (status != EntityCommandStatus.Running || SimulationClock.TimeD < _watchdog) return status;
        Cancel();
        return EntityCommandStatus.Interrupted;
    }

    public void Cancel()
    {
        if (_moveStarted)
        {
            _moveStarted = false;
            model.Planner?.Cancel(model);
            model.Motor.Stop();
        }
        OnCancel();
        Reward = InterruptionReward;
    }

    protected abstract EntityCommandStatus OnBegin();
    protected virtual EntityCommandStatus OnTick(float dt) => EntityCommandStatus.Completed;
    protected virtual void OnCancel() { }

    protected EntityCommandStatus Complete(float eventReward = 0f)
    {
        Reward = eventReward;
        return EntityCommandStatus.Completed;
    }

    protected EntityCommandStatus Fail()
    {
        Reward = FailureReward;
        return EntityCommandStatus.Failed;
    }

    protected EntityCommandStatus Interrupted()
    {
        Cancel();
        return EntityCommandStatus.Interrupted;
    }

    protected double HoldUntil(float seconds) => math.min(SimulationClock.TimeD + seconds, (double)decision.EndTime);

    protected double PauseUntil()
    {
        var r = SimulationRules.Active;
        float mul = model.IsHungry
            ? r.HungryPauseMul
            : math.lerp(1f, r.SatedPauseMul, model.Digestion != null ? model.Digestion.Fill : 0f);
        return HoldUntil(RavineRandom.RangeFloat(r.MovePauseMin, r.MovePauseMax) * mul);
    }

    protected static bool Elapsed(double time) => SimulationClock.TimeD >= time;

    protected static bool IsGone(EntityModel e) => e == null || e.IsDisposed || e.IsDeathPending;

    protected void StartMove(Vector3 target, float speed, float maxDuration, float energyCostPerSec)
    {
        double limit = SimulationClock.TimeD + maxDuration;
        _moveCut     = limit > _watchdog;
        _moveStarted = true;
        model.Motor.BeginMove(target, speed * model.SpeedMul, energyCostPerSec, _moveCut ? _watchdog : limit);
    }

    protected void StartPlannedMove(MoveIntent intent, float2 direction, float2 target, bool hasTarget,
        float radius, float speed, float maxDuration, float energyCostPerSec,
        float2 threat = default, bool hasThreat = false, float side = 0f, float curvature = float.NaN)
    {
        var planner = model.Planner;
        if (planner == null)
        {
            float2 end = hasTarget ? target : Extension.Flat(model.Motor.Position()) + math.normalizesafe(direction) * radius;
            StartMove(Extension.ToWorld(in end, model.Motor.Position().y), speed, maxDuration, energyCostPerSec);
            return;
        }

        double limit = SimulationClock.TimeD + maxDuration;
        _moveCut     = limit > _watchdog;
        _moveStarted = true;

        _plan = new PlanRequest
        {
            Origin    = Extension.Flat(model.Motor.Position()),
            Direction = direction,
            Target    = target,
            Threat    = threat,
            Radius    = radius,
            Curvature = float.IsNaN(curvature) ? decision.Curvature : curvature,
            Side      = side != 0f ? side : model.NextPlanSide(),
            Seed      = (uint)RavineRandom.RangeInt(1, int.MaxValue),
            Intent    = (byte)intent,
            HasTarget = hasTarget ? (byte)1 : (byte)0,
            HasThreat = hasThreat ? (byte)1 : (byte)0,
            ColonyIndex = (byte)model.ColonyIndex,
        };
        _planSpeed    = speed * model.SpeedMul;
        _planCost     = energyCostPerSec;
        _planDeadline = _moveCut ? _watchdog : limit;

        planner.Enqueue(model, in _plan, _planSpeed, _planCost, _planDeadline);
    }

    protected bool TryReplanBlocked(in MoveResult move)
    {
        var planner = model.Planner;
        if (!move.Blocked || planner == null || Elapsed(_planDeadline)) return false;
        if (_replans >= SimulationRules.Active.PlannerMaxReplans) return false;

        _replans++;
        float angle = SimulationRules.Active.PlannerReplanAngle * ((_replans & 1) == 0 ? -2f : 2f);
        math.sincos(angle, out float s, out float c);

        float2 d = math.normalizesafe(_plan.HasTarget != 0 ? _plan.Target - _plan.Origin : _plan.Direction, new float2(0f, 1f));
        _plan.Origin    = Extension.Flat(model.Motor.Position());
        _plan.Direction = new float2(d.x * c - d.y * s, d.x * s + d.y * c);
        _plan.HasTarget = 0;
        _plan.Side      = model.NextPlanSide();
        _plan.Seed      = (uint)RavineRandom.RangeInt(1, int.MaxValue);

        _moveStarted = true;
        planner.Enqueue(model, in _plan, _planSpeed, _planCost, _planDeadline);
        return true;
    }

    protected bool TryFinishMove(out MoveResult move, out bool cut)
    {
        var motor = model.Motor;
        if (model.PlanSlot >= 0 || motor.IsMoving)
        {
            move = default;
            cut  = false;
            return false;
        }

        _moveStarted = false;
        move = motor.LastMove;
        cut  = _moveCut && !move.Arrived && !move.Blocked;
        return true;
    }
}
