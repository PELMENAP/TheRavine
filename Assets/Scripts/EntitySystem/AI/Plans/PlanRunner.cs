using Unity.Mathematics;

public sealed class PlanRunner
{
    private const int MaxStartAttempts = 4;

    private readonly EntityModel _model;
    private PlanProgress  _p;
    private BrainDecision _decision;
    private double _start;
    private float  _return;
    private bool   _active;
    private bool   _ending;

    public PlanRunner(EntityModel model) => _model = model;

    public bool     IsActive => _active;
    public PlanKind Kind     => _active ? _p.Kind : PlanKind.Count;

    public void Begin(in BrainDecision decision)
    {
        _p = new PlanProgress { Kind = decision.Plan, Variant = decision.Action };
        _decision = decision;
        _start    = SimulationClock.TimeD;
        _return   = 0f;
        _active   = true;
        _ending   = false;

        _model.BeginHomeostasis();
        Launch(decision.Action, CommandSource.Brain);
    }

    public void Tick()
    {
        if (!_active || _ending) return;
        if (SimulationClock.Time >= _decision.PlanEnd) { Stop(EntityCommandStatus.Completed); return; }
        if (!_model.IsCommandRunning) Advance(EntityCommandStatus.Failed, -1);
    }

    public void Interrupt()
    {
        if (!_active || _ending) return;
        Stop(EntityCommandStatus.Interrupted);
    }

    public void OnStepFinished(EntityCommandStatus status, float reward, int action)
    {
        if (!_active) return;
        _return += DelayedPerceptron.DiscountTime(SimulationRules.Frame.ExecGammaPerSecond,
            (float)(SimulationClock.TimeD - _start)) * reward;
        if (_ending) return;
        Advance(status, action);
    }

    private void Advance(EntityCommandStatus status, int action)
    {
        if (!_model.IsAliveForPlan) { Stop(EntityCommandStatus.Interrupted); return; }
        if (SimulationClock.Time >= _decision.PlanEnd) { End(EntityCommandStatus.Completed); return; }

        for (int attempt = 0; attempt < MaxStartAttempts; attempt++)
        {
            int next = PlanCatalog.Next(ref _p, _model.PlanHints, status, action);
            if (next == PlanCatalog.Complete) { End(EntityCommandStatus.Completed); return; }
            if (next == PlanCatalog.Fail)     { End(EntityCommandStatus.Failed);    return; }

            if (Launch(next, CommandSource.Plan)) return;
            status = EntityCommandStatus.Failed;
            action = next;
        }

        End(EntityCommandStatus.Failed);
    }

    private bool Launch(int action, CommandSource source)
    {
        float duration = math.clamp(_decision.Duration, ActionDurationTable.Min(action), ActionDurationTable.Max(action));
        var step = _decision.WithStep(action, SimulationClock.Time, duration, source == CommandSource.Brain);
        if (_model.TryStartCommand(action, in step, source)) return true;
        if (_active && !_ending && source == CommandSource.Brain) Advance(EntityCommandStatus.Failed, action);
        return false;
    }

    private void Stop(EntityCommandStatus status)
    {
        _ending = true;
        _model.InterruptCommand();
        End(status);
    }

    private void End(EntityCommandStatus status)
    {
        if (!_active) return;
        _active = false;
        _ending = false;

        var r = SimulationRules.Active;
        float total = _return + _model.HomeostaticReturn() + _model.ConsumeExtrinsicReward();
        if (status == EntityCommandStatus.Completed) total += r.PlanCompleteReward;

        var brain = _model.Brain;
        brain.CompleteDecision(in _decision, total, SimulationClock.Time, status);
        brain.Context.GoalEndTime = 0f;
        _model.Colony?.Stats.RecordPlan(_p.Kind, status == EntityCommandStatus.Completed);
    }
}
