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
    private double _instantTick = double.NaN;
    private int    _instantSteps;
    private float  _storage0;
    private float  _population0;
    private float  _hunger0;
    private float  _stored0;

    public PlanRunner(EntityModel model) => _model = model;

    public bool     IsActive => _active;
    public PlanKind Kind     => _active ? _p.Kind : PlanKind.Count;

    public void Begin(in BrainDecision decision)
    {
        _p = new PlanProgress { Kind = decision.Plan, Variant = decision.Action };
        PlanCatalog.Init(ref _p);
        _decision = decision;
        _start    = SimulationClock.TimeD;
        _return   = 0f;
        _active   = true;
        _ending   = false;
        _instantSteps = 0;
        _instantTick  = SimulationClock.TimeD;

        var colony = _model.Colony;
        _storage0    = colony != null ? colony.StorageEma : 0f;
        _population0 = colony != null ? colony.PopulationEma : 0f;
        _hunger0     = colony != null ? colony.HungerEma : 0f;
        _stored0     = _model.StoredTotal;

        if (PlanCatalog.IsTravelPlan(_p.Kind) && decision.HasDirection)
            ResolveDestination(decision.Direction, decision.DirOffset);

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

        double now = SimulationClock.TimeD;
        if (now != _instantTick) { _instantTick = now; _instantSteps = 0; }
        if (++_instantSteps > SimulationRules.Active.PlanMaxInstantSteps) { End(EntityCommandStatus.Failed); return; }
        if (SimulationClock.Time >= _decision.PlanEnd) { End(EntityCommandStatus.Completed); return; }

        for (int attempt = 0; attempt < MaxStartAttempts; attempt++)
        {
            int next = PlanCatalog.Next(ref _p, CurrentHints(), status, action);
            if (_p.NeedsDestination)
            {
                _p.NeedsDestination = false;
                byte dir = _p.Direction != (byte)DirectionKind.None ? _p.Direction : _decision.Direction;
                ResolveDestination(dir, _decision.DirOffset);
            }
            if (next == PlanCatalog.Complete) { End(EntityCommandStatus.Completed); return; }
            if (next == PlanCatalog.Fail)     { End(EntityCommandStatus.Failed);    return; }

            if (Launch(next, CommandSource.Plan)) return;
            status = EntityCommandStatus.Failed;
            action = next;
        }

        End(EntityCommandStatus.Failed);
    }

    private PlanHint CurrentHints()
    {
        var h = _model.PlanHints;
        if (_p.HasDestination
            && math.distance(_model.Position2D, _p.Destination) <= SimulationRules.Frame.PlanArriveRadius)
            h |= PlanHint.AtDestination;
        return h;
    }

    private void ResolveDestination(byte direction, float offset)
    {
        _p.HasDestination = false;
        _p.Hops = 0;
        if (!_model.TryResolveDirection(direction, offset, out float2 dir, out float radius)) return;
        ref readonly var r = ref SimulationRules.Frame;
        bool  anchored = direction <= (byte)DirectionKind.ColonyPoi;
        float maxTravel = math.max(r.PlanMinTravel, r.PlanMaxTravel);
        float travel = anchored ? math.min(radius, maxTravel) : math.clamp(radius, r.PlanMinTravel, maxTravel);
        _p.Destination    = _model.Position2D + dir * travel;
        _p.HasDestination = true;
    }

    private float ColonyReward()
    {
        var colony = _model.Colony;
        if (colony == null || colony.IsWild || colony.MemberCount == 0) return 0f;
        ref readonly var r = ref SimulationRules.Frame;

        float own      = (_model.StoredTotal - _stored0) / math.max(r.NestStorageNorm, 1e-3f);
        float dStorage = colony.StorageEma - _storage0 - own / colony.MemberCount;
        float dPop     = (colony.PopulationEma - _population0) / math.max(r.ColonyPopulationNorm, 1f);
        float dHunger  = colony.HungerEma - _hunger0;
        return r.ColonyRewardWeight
             * (dStorage + r.ColonyPopulationWeight * dPop - r.ColonyHungerWeight * dHunger) / colony.MemberCount;
    }

    private bool Launch(int action, CommandSource source)
    {
        ref readonly var r = ref SimulationRules.Frame;
        float tempo = _model.Virology != null ? _model.Virology.Modifiers.StepTempo : 0f;
        float scale = 1f + math.max(tempo, 0f) * r.PersistStepMul - math.max(-tempo, 0f) * r.ImpulseStepMul;
        float duration = math.clamp(_decision.Duration * math.max(scale, 0.05f),
            ActionDurationTable.Min(action), ActionDurationTable.Max(action));
        var step = _decision.WithStep(action, SimulationClock.Time, duration, source == CommandSource.Brain);
        if (_p.Direction != (byte)DirectionKind.None) step = step.WithDirection(_p.Direction);
        if (_p.HasDestination && action == (int)EntityAction.Wander) step = step.WithDestination(_p.Destination);
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
        float total = _return + _model.HomeostaticReturn() + _model.ConsumeExtrinsicReward() + ColonyReward();
        if (_p.Achieved && status != EntityCommandStatus.Failed) total += r.PlanCompleteReward;
        _model.OnPlanEnded(_p.Kind, total, _p.Achieved);

        var brain = _model.Brain;
        brain.CompleteDecision(in _decision, total, SimulationClock.Time, status);
        brain.Context.GoalEndTime = 0f;
        var stats = _model.Colony?.Stats;
        stats?.RecordPlan(_p.Kind, status == EntityCommandStatus.Completed);
        if (_model.CasteFixed && !_model.IsJuvenile) stats?.RecordCastePlan(_model.Caste, _p.Kind);
    }
}
