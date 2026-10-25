using Unity.Mathematics;

public class AttackCommand : EntityCommand
{
    private enum Phase { Chase, Dash, Ready, Recover }

    private static readonly EntityModel[] Allies = new EntityModel[16];
    private HuntTarget _target;
    private Phase  _phase;
    private double _recoverEnd;
    private float2 _chaseGoal;

    public AttackCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.Stats.En >= math.max(model.Tuning.AttackEnergyCost, SimulationRules.Active.AttackEnergyMin);

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
        _target = model.CachedHuntTarget;
        if (!_target.IsValid || !CanExecute()) return Fail();
        return Engage(SimulationRules.Active);
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        var r = SimulationRules.Active;

        if (_phase == Phase.Recover)
            return Elapsed(_recoverEnd) ? Complete() : EntityCommandStatus.Running;

        if (!_target.IsValid) return Fail();
        float range = model.Tuning.AttackRange;
        float d     = DistanceToTarget();

        switch (_phase)
        {
            case Phase.Ready:
                if (d > range) return Engage(r);
                return model.AttackReady ? Strike(r) : EntityCommandStatus.Running;

            case Phase.Chase:
                if (d <= range + r.DashDistance) return Engage(r);
                if (!TryFinishMove(out _, out bool cutChase))
                {
                    if (math.distance(_chaseGoal, _target.Position2D) > r.DashDistance) return Chase(r);
                    return EntityCommandStatus.Running;
                }
                return cutChase ? Interrupted() : Chase(r);

            default:
                if (!TryFinishMove(out _, out bool cut)) return d <= range ? Engage(r) : EntityCommandStatus.Running;
                return cut ? Interrupted() : Engage(r);
        }
    }

    private EntityCommandStatus Engage(SimulationRules r)
    {
        float d     = DistanceToTarget();
        float range = model.Tuning.AttackRange;
        if (d <= range)
        {
            model.Motor.Stop();
            _phase = Phase.Ready;
            return model.AttackReady ? Strike(r) : EntityCommandStatus.Running;
        }
        return d <= range + r.DashDistance ? BeginDash(r) : Chase(r);
    }

    private EntityCommandStatus Chase(SimulationRules r)
    {
        _phase     = Phase.Chase;
        _chaseGoal = _target.Position2D;
        StartMove(_target.Position, model.Tuning.RunSpeed, r.AttackMoveMaxDuration, model.Tuning.EnergyCostRunning);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus BeginDash(SimulationRules r)
    {
        var stats = model.Stats;
        if (stats.En < math.max(model.Tuning.AttackEnergyCost, r.AttackEnergyMin)) return Fail();

        stats.En = math.max(0f, stats.En - r.DashEnergyCost);
        _phase = Phase.Dash;

        float speed = model.Tuning.RunSpeed * r.DashMul;
        StartMove(_target.Position, speed, r.DashDistance / math.max(speed * model.SpeedMul, r.TimingMinSpeed) + r.DashRecovery,
            model.Tuning.EnergyCostRunning);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus Strike(SimulationRules r)
    {
        var target = _target;
        if (!target.IsValid) return Fail();

        var stats = model.Stats;
        float cost = model.Tuning.AttackEnergyCost;
        if (stats.En < cost) return Fail();
        if (!model.TryStartAttackCooldown()) return EntityCommandStatus.Running;

        float energyScale = math.max(math.saturate(stats.En / stats.MaxEnergy), r.AttackMinEnergyScale);
        stats.En -= cost;

        float damage = model.Tuning.AttackDamage * model.AttackDamageMul * energyScale;
        if (model.SinceAction(EntityAction.Threaten) < r.SynergyWindow) damage *= r.ThreatenAttackMul;
        damage *= 1f + r.GroupAttackBonus * model.CasteMods.GroupBonus * CountAllies(in target, r);

        var victim = target.Entity;
        if (victim != null)
        {
            victim.TakeDamage(damage, model);
            model.Infection?.TryTransmitBite(model, victim);
        }
        else model.Players?.ReportHit(target.Player, damage, model);
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.DamageDealt, damage);
        model.Colony?.Stats.RecordAttack(victim == null ? ColonyStats.AttackTarget.Player
            : victim.Colony == model.Colony ? ColonyStats.AttackTarget.Own : ColonyStats.AttackTarget.Foreign);

        _phase      = Phase.Recover;
        _recoverEnd = SimulationClock.TimeD + r.DashRecovery;
        model.Motor.Stop();
        return EntityCommandStatus.Running;
    }

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

    protected override void OnCancel() => _target = default;
}
