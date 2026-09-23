using Unity.Mathematics;
using UnityEngine;

public interface IEntityCommand
{
    float Reward { get; }
    bool CanExecute();
    EntityCommandStatus Begin(in BrainDecision decision);
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

    public float Reward { get; private set; }

    protected EntityCommand(EntityModel m) => model = m;

    public virtual bool CanExecute() => true;
    protected virtual float InterruptionReward => SimulationRules.Active.InterruptionReward;
    protected virtual float FailureReward      => SimulationRules.Active.FailureReward;

    protected static float PathCostPenalty(in MoveResult move)
    {
        if (move.Distance <= 1e-3f) return 0f;

        ref readonly var r = ref SimulationRules.Frame;
        float ratio = math.min(move.CostPerUnit, r.PathCostRatioMax);
        return (ratio - 1f) * r.PathCostPenalty;
    }

    public EntityCommandStatus Begin(in BrainDecision d)
    {
        decision     = d;
        _watchdog    = d.EndTime + SimulationRules.Active.CommandWatchdogGrace;
        _moveStarted = false;
        _moveCut     = false;
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
            model.Motor.Stop();
        }
        OnCancel();
        Reward = InterruptionReward;
    }

    protected abstract EntityCommandStatus OnBegin();
    protected virtual EntityCommandStatus OnTick(float dt) => EntityCommandStatus.Completed;
    protected virtual void OnCancel() { }

    protected EntityCommandStatus Complete(float reward)
    {
        Reward = reward;
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

    protected static bool Elapsed(double time) => SimulationClock.TimeD >= time;

    protected static bool IsGone(EntityModel e) => e == null || e.IsDisposed || e.IsDeathPending;

    protected void StartMove(Vector3 target, float speed, float maxDuration, float energyCostPerSec)
    {
        double limit = SimulationClock.TimeD + maxDuration;
        _moveCut     = limit > _watchdog;
        _moveStarted = true;
        model.Motor.BeginMove(target, speed, energyCostPerSec, _moveCut ? _watchdog : limit);
    }

    protected bool TryFinishMove(out MoveResult move, out bool cut)
    {
        var motor = model.Motor;
        if (motor.IsMoving)
        {
            move = default;
            cut  = false;
            return false;
        }

        _moveStarted = false;
        move = motor.LastMove;
        cut  = _moveCut && !move.Arrived;
        return true;
    }
}