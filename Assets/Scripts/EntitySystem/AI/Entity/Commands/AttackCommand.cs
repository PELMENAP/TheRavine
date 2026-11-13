using Unity.Mathematics;

public class AttackCommand : EntityCommand
{
    private enum Phase { Approach, Lurk, Dash, Ready, Recover }

    private static readonly EntityModel[] Allies = new EntityModel[16];
    private HuntTarget  _target;
    private Phase       _phase;
    private AttackStyle _style;
    private double _recoverEnd;
    private double _lurkStart;
    private double _lurkEnd;
    private float2 _chaseGoal;
    private float  _damageShare;
    private float  _trigger;
    private float  _approachDist;
    private float  _approachCredit;
    private bool   _dashed;
    private bool   _ambushed;
    private bool   _closing;

    public AttackCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
    {
        var tuning = model.Tuning;
        return model.Stats.En >= EnergyBudget(tuning.AttackEnergyCost, tuning.EnergyCostRunning, tuning.RunSpeed,
            model.SpeedMul, model.HuntTargetDistance, in SimulationRules.Frame);
    }

    public static float EnergyBudget(float attackCost, float runCost, float runSpeed, float speedMul, float distance,
        in SimulationRules.RulesFrame r)
    {
        float speed = math.max(runSpeed * speedMul, r.TimingMinSpeed);
        float need  = (attackCost + math.max(distance, 0f) * runCost / speed + r.DashEnergyCost) * r.AttackBudgetMargin;
        return math.max(r.AttackEnergyMin, need);
    }

    protected override float MinRuntime
    {
        get
        {
            var r = SimulationRules.Active;
            float dash = r.DashDistance / math.max(model.Tuning.RunSpeed * r.DashMul * model.SpeedMul, r.TimingMinSpeed);
            return r.AttackMoveMaxDuration + dash + 2f * r.DashRecovery;
        }
    }

    protected override EntityCommandStatus OnBegin()
    {
        _target         = model.CachedHuntTarget;
        _damageShare    = 0f;
        _dashed         = false;
        _ambushed       = false;
        _closing        = false;
        _approachCredit = 0f;
        if (!_target.IsValid)
        {
            model.Colony?.Stats.RecordAttackFailNoTarget();
            return FailCredited();
        }
        if (!CanExecute())
        {
            model.Colony?.Stats.RecordAttackFailLowEnergy();
            return FailCredited();
        }

        var r = SimulationRules.Active;
        _style   = decision.Style < AttackStyle.Count ? decision.Style : AttackStyle.Pursue;
        float t  = math.saturate(decision.Tempo * 0.5f + 0.5f);
        _trigger = r.DashDistance * math.lerp(r.AttackTriggerMinMul, math.max(r.AttackTriggerMaxMul, r.AttackTriggerMinMul), t);

        if (_style == AttackStyle.Ambush && DistanceToTarget() > model.Tuning.AttackRange + _trigger) return Lurk(r);
        return Engage(r);
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        var r = SimulationRules.Active;

        if (_phase == Phase.Recover)
            return Elapsed(_recoverEnd) ? Complete(r.DamageEventReward * _damageShare + _approachCredit) : EntityCommandStatus.Running;

        if (!_target.IsValid)
        {
            model.Colony?.Stats.RecordAttackFailTargetLost();
            return FailCredited();
        }
        float range = model.Tuning.AttackRange;
        float d     = DistanceToTarget();

        switch (_phase)
        {
            case Phase.Lurk:
                if (d <= range + _trigger)
                {
                    _ambushed = SimulationClock.TimeD - _lurkStart >= r.AmbushMinHold;
                    return Engage(r);
                }
                if (d > model.Tuning.DetectionRadius)
                {
                    model.Colony?.Stats.RecordAttackFailLurkTimeout();
                    return FailCredited();
                }
                if (Elapsed(_lurkEnd)) return Complete(_approachCredit);
                return EntityCommandStatus.Running;

            case Phase.Ready:
                if (d > range) return Engage(r);
                return model.AttackReady ? Strike(r) : Contact(r, d, range);

            case Phase.Approach:
                _approachCredit += math.max(0f, _approachDist - d) * r.ApproachRewardWeight;
                _approachDist    = d;
                if (d <= range || (d <= range + _trigger && CanDash(r))) return Engage(r);
                if (!TryFinishMove(out _, out bool cutChase))
                {
                    if (math.distance(_chaseGoal, _target.Position2D) > r.DashDistance) return Approach(r);
                    return EntityCommandStatus.Running;
                }
                return cutChase ? Interrupted() : Approach(r);

            default:
                if (!TryFinishMove(out _, out bool cut)) return d <= range ? Engage(r) : EntityCommandStatus.Running;
                return cut ? Interrupted() : Engage(r);
        }
    }

    private EntityCommandStatus Lurk(SimulationRules r)
    {
        model.Motor.Stop();
        _phase     = Phase.Lurk;
        _lurkStart = SimulationClock.TimeD;
        float patience = math.saturate(decision.Duration / math.max(ActionDurationTable.Max((int)EntityAction.Attack), 1e-3f));
        _lurkEnd   = _lurkStart + r.AmbushMaxHold * patience;
        ExtendWatchdog(_lurkEnd);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus Engage(SimulationRules r)
    {
        _closing = false;
        float d     = DistanceToTarget();
        float range = model.Tuning.AttackRange;
        if (d <= range)
        {
            model.Motor.Stop();
            _phase = Phase.Ready;
            return model.AttackReady ? Strike(r) : Contact(r, d, range);
        }
        return d <= range + _trigger && CanDash(r) ? BeginDash(r) : Approach(r);
    }

    private EntityCommandStatus Contact(SimulationRules r, float d, float range)
    {
        ExtendWatchdog(model.AttackReadyTime);
        bool far = d > range * r.AttackContactFraction;
        if (_closing)
        {
            if (!far)
            {
                model.Motor.Stop();
                _closing = false;
            }
            else if (TryFinishMove(out _, out _)) _closing = false;
            return EntityCommandStatus.Running;
        }
        if (!far) return EntityCommandStatus.Running;

        _closing = true;
        StartMove(_target.Position, model.Tuning.MoveSpeed, r.AttackMoveMaxDuration, model.Tuning.EnergyCostMoving);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus Approach(SimulationRules r)
    {
        _phase        = Phase.Approach;
        _chaseGoal    = _target.Position2D;
        _approachDist = DistanceToTarget();
        bool walk     = _style == AttackStyle.Stalk;
        StartMove(_target.Position, walk ? model.Tuning.MoveSpeed : model.Tuning.RunSpeed, r.AttackMoveMaxDuration,
            walk ? model.Tuning.EnergyCostMoving : model.Tuning.EnergyCostRunning);
        return EntityCommandStatus.Running;
    }

    private bool CanDash(SimulationRules r)
        => model.Stats.En >= math.max(model.Tuning.AttackEnergyCost, r.AttackEnergyMin) + r.DashEnergyCost;

    private EntityCommandStatus BeginDash(SimulationRules r)
    {
        var stats = model.Stats;
        float enBefore = stats.En;
        stats.En = math.max(0f, stats.En - r.DashEnergyCost);
        model.Ledger.Flows.Dash += enBefore - stats.En;
        _phase  = Phase.Dash;
        _dashed = true;

        float speed = model.Tuning.RunSpeed * r.DashMul;
        StartMove(_target.Position, speed, r.DashDistance / math.max(speed * model.SpeedMul, r.TimingMinSpeed) + r.DashRecovery,
            model.Tuning.EnergyCostRunning);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus Strike(SimulationRules r)
    {
        var target = _target;
        if (!target.IsValid)
        {
            model.Colony?.Stats.RecordAttackFailTargetLost();
            return FailCredited();
        }

        var stats = model.Stats;
        float cost = model.Tuning.AttackEnergyCost;
        if (stats.En < cost)
        {
            model.Colony?.Stats.RecordAttackFailLowEnergy();
            return FailCredited();
        }
        if (!model.TryStartAttackCooldown()) return EntityCommandStatus.Running;

        float energyScale = math.max(math.saturate(stats.En / stats.MaxEnergy), r.AttackMinEnergyScale);
        stats.En -= cost;
        model.Ledger.Flows.Strike += cost;

        float damage = model.Tuning.AttackDamage * model.AttackDamageMul * energyScale;
        if (model.SinceThreatDisplay < r.SynergyWindow) damage *= r.ThreatenAttackMul;
        if (_ambushed) damage *= r.AmbushDamageMul;
        if (_dashed)   damage *= r.DashStrikeMul;
        _ambushed = false;
        _dashed   = false;
        damage *= 1f + r.GroupAttackBonus * model.CasteMods.GroupBonus * CountAllies(in target, r);

        var victim = target.Entity;
        if (victim != null)
        {
            victim.TakeDamage(damage, model);
            model.Infection?.TryTransmitBite(model, victim);
        }
        else model.Players?.ReportHit(target.Player, damage, model);
        var victimStats = victim != null ? victim.Stats : model.Players?.StatsOf(target.Player);
        bool outside    = victim == null || victim.Colony != model.Colony;
        if (outside && victimStats != null && victimStats.MaxHealth > 0f) _damageShare += damage / victimStats.MaxHealth;
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.DamageDealt, damage);
        model.Colony?.Stats.RecordAttack(victim == null ? ColonyStats.AttackTarget.Player
            : victim.Colony == model.Colony ? ColonyStats.AttackTarget.Own : ColonyStats.AttackTarget.Foreign);

        _phase      = Phase.Recover;
        _recoverEnd = SimulationClock.TimeD + r.DashRecovery;
        model.Motor.Stop();
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus FailCredited() => Fail(FailureReward + _approachCredit);

    private float DistanceToTarget() => math.distance(model.Position2D, _target.Position2D);

    private int CountAllies(in HuntTarget target, SimulationRules r)
    {
        int found = model.Perception.FindEntitiesInRadius(target.Position, target.Entity ?? model, r.GroupAttackRadius, Allies);
        int n = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Allies[i];
            Allies[i] = null;
            if (e == null || ReferenceEquals(e, model) || e.SinceAction(EntityAction.Attack) > r.GroupAttackWindow) continue;
            n++;
        }
        return math.min(n, r.GroupAttackMaxCount);
    }

    protected override void OnCancel()
    {
        _target  = default;
        _closing = false;
    }
}
