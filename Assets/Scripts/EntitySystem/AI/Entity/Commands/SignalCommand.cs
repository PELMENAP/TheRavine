using Unity.Mathematics;

using TheRavine.Extensions;

public class SignalCommand : EntityCommand
{
    private enum Kind : byte { Mark, Utter, Threaten }

    private Kind       _kind;
    private double     _until;
    private HuntTarget _target;
    private float2     _facing;

    public SignalCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        switch (decision.Goal)
        {
            case SharedHierarchicalBrain.Goal.Social: return BeginUtter();
            case SharedHierarchicalBrain.Goal.Hunt:   return BeginThreaten();
            default:                                  return BeginMark();
        }
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        switch (_kind)
        {
            case Kind.Utter:    return Elapsed(_until) ? Complete() : EntityCommandStatus.Running;
            case Kind.Threaten: return TickThreaten();
            default:            return Complete();
        }
    }

    protected override void OnCancel() => _target = default;

    private EntityCommandStatus BeginMark()
    {
        _kind = Kind.Mark;
        var r = SimulationRules.Active;
        float2 pos = model.Position2D;
        bool added = model.Points.TryRemember(in pos, r.RememberPointMinSpacing);

        var colony = model.Colony;
        if (colony != null && !colony.IsWild && model.Points.TryGetNear(in pos, r.RememberPointMinSpacing, out var point))
            colony.Pois.Offer(point.Position, point.EnergyEma * model.CasteMods.PoiWeight, 0f);
        return added && model.CachedFoodValid ? Complete(r.RememberFoodReward) : Complete();
    }

    private EntityCommandStatus BeginUtter()
    {
        _kind = Kind.Utter;
        float cost = SimulationRules.Active.SpeechEnergyCost;
        var stats = model.Stats;
        if (decision.SpeechLen == 0 || stats.En < cost || !model.TryStartSpeechCooldown()) return Fail();

        stats.En -= cost;
        model.Ledger.Flows.Speech += cost;

        var utterance = model.MakeUtterance(decision.Tokens, decision.SpeechLen);
        model.Emit(in utterance);
        model.NoteSpeechDecision(in decision);

        _until = SimulationClock.TimeD + decision.SpeechLen * SimulationRules.Frame.SpeechSyllableSeconds;
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus BeginThreaten()
    {
        _kind = Kind.Threaten;
        var r = SimulationRules.Active;
        var target = model.CachedHuntTarget;
        if (!target.IsValid) return Fail();
        float dist = math.distance(model.Position2D, target.Position2D);
        if (dist > model.Tuning.AttackRange * r.ThreatenRangeMul) return Fail();

        var stats = model.Stats;
        float enBefore = stats.En;
        stats.En = math.max(0f, stats.En - r.ThreatenEnergyCost);
        model.Ledger.Flows.Threaten += enBefore - stats.En;
        model.NoteThreatDisplay();
        _target  = target;
        _facing  = float2.zero;
        _until   = HoldUntil(r.ThreatenDuration);
        Face(r);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus TickThreaten()
    {
        if (Elapsed(_until)) return Complete();
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
            (float)math.max(_until - SimulationClock.TimeD, r.ThreatenMinHoldSeconds), model.Tuning.EnergyCostMoving);
    }
}
