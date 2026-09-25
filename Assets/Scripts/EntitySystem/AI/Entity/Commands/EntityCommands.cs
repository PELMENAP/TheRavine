using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;
using TheRavine.EntityControl.Virology;

public class RestCommand : EntityCommand
{
    private double _prev;

    public RestCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.Motor.Stop();
        _prev = SimulationClock.TimeD;
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        var r     = SimulationRules.Active;
        var stats = model.Stats;

        double end = decision.EndTime;
        double t   = math.min(SimulationClock.TimeD, end);
        float step = (float)(t - _prev);
        if (step > 0f)
        {
            _prev = t;
            float hp     = stats.Hp;
            float en     = stats.En;
            float cost   = r.RestHealEnergyCost;
            float rate   = r.RestHealRate * (model.IsAtNest ? r.NestRestHealMul : 1f) * model.RestHealMul;
            float budget = cost > 0f ? en / cost : float.MaxValue;
            float heal   = math.min(math.min(rate * step, stats.MaxHealth - hp), budget);
            if (heal > 0f)
            {
                stats.Hp = hp + heal;
                stats.En = math.max(0f, en - heal * cost);
            }
        }

        if (t < end) return EntityCommandStatus.Running;

        model.Brain.Context.GoalRestCount++;
        return Complete();
    }
}

public class IdleCommand : EntityCommand
{
    public IdleCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.Motor.Stop();
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(decision.EndTime) ? Complete() : EntityCommandStatus.Running;
}

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

public class WanderCommand : PlannedMoveCommand
{
    public WanderCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        float2 desired;
        float  radius = model.Tuning.WanderRadius;

        var nest = model.Nest;
        if (decision.Plan == PlanKind.Migrate && nest != null && nest.MigrationPressure > 1e-4f)
        {
            desired = math.normalizesafe(nest.Migration);
            radius  = math.min(r.MigrateLegRadius, r.PlannerRadiusMax);
        }
        else if (decision.HasHeading) desired = decision.Heading;
        else if (!TryFieldGradient(r, radius, out desired))
        {
            float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
            math.sincos(a, out float sa, out float ca);
            desired = new float2(ca, sa);
            radius  = math.min(LevyStep(r), r.PlannerRadiusMax);
        }

        float2 dir = TerrainSteering.Blend(in desired, in model.LastTerrain);
        return BeginPlanned(MoveIntent.Wander, dir, default, false,
            radius, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    private bool TryFieldGradient(SimulationRules r, float radius, out float2 dir)
    {
        dir = float2.zero;
        var nest = model.Nest;
        if (nest == null) return false;

        float phase  = RavineRandom.RangeFloat(0f, 2f * math.PI);
        float spread = nest.SampleDirection(model.Position2D, radius, r.WanderGradientSamples, phase, 1f, 1f, out dir);
        return spread > r.WanderGradientMin;
    }

    private static float LevyStep(SimulationRules r)
    {
        float u = RavineRandom.RangeFloat(1e-4f, 1f);
        float l = r.LevyMinStep * math.pow(u, -1f / math.max(r.LevyAlpha, 1e-3f));
        return math.clamp(l, r.LevyMinStep, r.LevyMaxStep);
    }
}
public class ApproachFoodCommand : PlannedMoveCommand
{
    public ApproachFoodCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    public override bool CanExecute() => model.CachedFoodValid;

    protected override EntityCommandStatus OnBegin()
    {
        if (!model.CachedFoodValid) return Fail();
        if (model.CachedFoodDistance <= SimulationRules.Active.EatRange) return Complete();

        float2 food = ChunkFoodIndex.CellCenter(model.CachedFoodCell);
        return BeginPlanned(MoveIntent.ApproachFood, food - model.Position2D, food, true,
            model.Tuning.DetectionRadius, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }
}

public class ReturnNestCommand : PlannedMoveCommand
{
    public ReturnNestCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    public override bool CanExecute() => model.Nest != null;

    protected override EntityCommandStatus OnBegin()
    {
        var nest = model.Nest;
        if (nest == null) return Fail();
        if (model.IsAtNest) return Complete();

        float2 home = nest.Position;
        return BeginPlanned(MoveIntent.ReturnNest, home - model.Position2D, home, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }
}

public class GoToPointCommand : PlannedMoveCommand
{
    public GoToPointCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => model.Points.Count > 0 || (model.Colony != null && model.Colony.Pois.Count > 0);

    protected override EntityCommandStatus OnBegin()
    {
        float2 self = model.Position2D;
        bool own = model.Points.TryPickBest(in self, out float2 target);
        var colony = model.Colony;
        if (colony != null && colony.Pois.TryPickBest(self, out float2 shared, out _)
            && (!own || math.distancesq(shared, self) < math.distancesq(target, self)))
        {
            target = shared;
            own = true;
        }
        if (!own) return Fail();

        return BeginPlanned(MoveIntent.GoToPOI, target - self, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        float2 self = model.Position2D;
        model.Points.Visit(in self);
        return Complete();
    }
}
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

        var threat = model.CachedNearest;
        if (threat != null) _threat = Extension.Flat(threat.Motor.Position());
        else if (!TryDangerSource(r, self, total, out _threat)) return Complete();

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
        if (++_leg >= _legs || _legLength <= 0.5f) return Complete();

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
public class EatCommand : EntityCommand
{
    private enum Phase { Waiting, Eating }

    private Phase  _phase;
    private float  _portion;
    private float  _eaten;
    private double _start;
    private double _end;
    private bool   _toxic;

    public EatCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => !model.IsSated;

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        model.Motor.Stop();

        if (model.Carrying > 0f)
            return StartEating(model.TakeCarried(), false);

        var nest  = model.Nest;
        var index = model.FoodIndex;
        bool foodNear = index != null && model.CachedFoodValid && model.CachedFoodDistance <= r.EatRange;

        if (!foodNear && nest != null && model.IsAtNest && nest.Storage > 0f)
        {
            float take = math.min(nest.Storage, r.NestEatPortion);
            nest.Storage -= take;
            return StartEating(take, false);
        }

        if (!foodNear) return Fail();

        long cell = model.CachedFoodCell;
        model.InvalidateCachedFood();
        if (!index.TryClaim(cell, model, SimulationClock.TimeD, out var claim)) return Fail();

        if (claim.Infected && index.TryTakePayload(cell, out var payload))
        {
            model.Infection?.InfectFromPayload(model, in payload);
            ViralPayloadPool.Release(ref payload);
        }

        if (claim.Pending)
        {
            _phase = Phase.Waiting;
            _end   = HoldUntil(r.LargeFoodWindow);
            model.ConsumeReceivedFood();
            return EntityCommandStatus.Running;
        }

        return StartEating(claim.Energy, claim.Kind == FoodKind.Toxic);
    }

    private EntityCommandStatus StartEating(float portion, bool toxic)
    {
        if (portion <= 0f) return Fail();

        var r = SimulationRules.Active;
        _phase   = Phase.Eating;
        _portion = portion;
        _eaten   = 0f;
        _toxic   = toxic;
        _start   = SimulationClock.TimeD;
        _end     = _start + math.max(r.EatSecondsPerEnergy * portion, 1e-3f);

        model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
        model.Brain.Context.GoalFoodEaten++;
        float2 here = model.Position2D;
        model.Points.CreditEnergy(in here, portion);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (_phase == Phase.Waiting)
        {
            if (model.ConsumeReceivedFood())
            {
                model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
                model.Brain.Context.GoalFoodEaten++;
                return Complete();
            }
            return Elapsed(_end) ? Fail() : EntityCommandStatus.Running;
        }

        Deliver();
        if (!Elapsed(_end)) return EntityCommandStatus.Running;

        if (_toxic) model.TakeDamage(SimulationRules.Active.ToxicDamage, null, DeathCause.Toxic);
        return Complete();
    }

    private void Deliver()
    {
        double span = _end - _start;
        float frac  = span > 0d ? (float)math.saturate((SimulationClock.TimeD - _start) / span) : 1f;
        float due   = _portion * frac - _eaten;
        if (due <= 0f) return;
        model.Digestion.Ingest(due);
        _eaten += due;
    }

    protected override void OnCancel()
    {
        if (_phase == Phase.Eating) Deliver();
        _portion = 0f;
    }
}

public class PickUpCommand : EntityCommand
{
    public PickUpCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.Carrying < model.CarryCapacity * SimulationRules.Active.CarryFullFraction;

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var index = model.FoodIndex;
        if (index == null || !model.CachedFoodValid || model.CachedFoodDistance > r.EatRange) return Fail();

        long cell = model.CachedFoodCell;
        if (!index.TryGetKind(cell, out FoodKind kind, out _) || kind == FoodKind.Large) return Fail();

        model.InvalidateCachedFood();
        if (!index.TryClaim(cell, model, SimulationClock.TimeD, out var claim) || claim.Energy <= 0f) return Fail();
        if (claim.Infected && index.TryTakePayload(cell, out var payload)) ViralPayloadPool.Release(ref payload);

        model.Carry(claim.Energy);
        return Complete();
    }
}

public class StoreFoodCommand : EntityCommand
{
    public StoreFoodCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => model.Carrying > 0f && model.IsAtNest;

    protected override EntityCommandStatus OnBegin()
    {
        var nest = model.Nest;
        if (nest == null || !model.IsAtNest || model.Carrying <= 0f) return Fail();
        nest.Storage += model.TakeCarried();
        model.DepositTrail();
        model.ShareMemoryWithColony();
        return Complete();
    }
}

public class RememberPointCommand : EntityCommand
{
    public RememberPointCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        float2 pos = model.Position2D;
        model.Points.TryRemember(in pos, SimulationRules.Active.RememberPointMinSpacing);
        if (model.Caste == Caste.Scout) model.Colony?.Pois.Offer(pos, model.CachedFoodValid ? SimulationRules.Active.EatEnergyFood : 0f, 0f);
        return Complete();
    }
}

public class ReproduceCommand : EntityCommand
{
    private double _holdEnd;

    public ReproduceCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() =>
        model.Stats.En >= model.Tuning.ReproduceEnergyCost &&
        model.Stats.Hp >= model.Tuning.ReproduceHealthCost;

    protected override EntityCommandStatus OnBegin()
    {
        model.Stats.En -= model.Tuning.ReproduceEnergyCost;
        model.Stats.Hp -= model.Tuning.ReproduceHealthCost;
        model.RequestReproduce();
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.Reproduced);

        _holdEnd = HoldUntil(model.Tuning.IdleTime);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete(SimulationRules.Active.EventReproduceReward) : EntityCommandStatus.Running;
}

public class SpeechCommand : EntityCommand
{
    private CancellationTokenSource _cts;
    private int  _generation;
    private bool _playing;
    private bool _failed;

    public SpeechCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var stats = model.Stats;
        stats.En = math.max(0f, stats.En - r.SpeechEnergyCost);

        model.Broadcast(decision.Speech);

        string hash = SpeechComponent.Encode(decision.Speech);
        DialogSystem.Instance.OnSpeechSend((IDialogSender)model.Motor, hash);

        if (_cts == null || _cts.IsCancellationRequested)
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        _failed = false;
        PlayAsync(hash, ++_generation, _cts.Token).Forget();
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (_playing) return EntityCommandStatus.Running;
        return _failed ? Fail() : Complete();
    }

    protected override void OnCancel()
    {
        if (_playing) _cts?.Cancel();
        _playing = false;
    }

    private async UniTaskVoid PlayAsync(string hash, int generation, CancellationToken ct)
    {
        _playing = true;
        try
        {
            await model.Speech.PlayAsync(
                hash, model.Stats.Hp, model.Stats.En,
                0f, 0f, model.LastActionIndex, model.CachedNearestDistance, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (generation == _generation) _failed = true;
        }
        finally
        {
            if (generation == _generation) _playing = false;
        }
    }
}

public class MimicCommand : EntityCommand
{
    public MimicCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var other = model.CachedNearest;
        if (other == null) return Fail();

        model.SetMimickedAction(other.LastActionIndex);
        return Complete();
    }
}

public class ThreatenCommand : EntityCommand
{
    private double      _holdEnd;
    private EntityModel _target;
    private float2      _facing;

    public ThreatenCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var target = model.CachedHuntTarget;
        if (target == null) return Fail();
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
        if (IsGone(_target)) return Complete();
        if (!model.Motor.IsMoving) Face(SimulationRules.Active);
        return EntityCommandStatus.Running;
    }

    private void Face(SimulationRules r)
    {
        float2 self = model.Position2D;
        float2 to   = math.normalizesafe(Extension.Flat(_target.Motor.Position()) - self);
        if (math.lengthsq(to) < 1e-6f) return;

        bool first = math.lengthsq(_facing) < 1e-6f;
        if (!first && math.dot(to, _facing) >= math.cos(r.ThreatenReorientAngle)) return;

        _facing = to;
        float2 step = self + to * r.ThreatenStepDistance;
        StartMove(Extension.ToWorld(in step, model.Motor.Position().y), model.Tuning.MoveSpeed,
            (float)math.max(_holdEnd - SimulationClock.TimeD, 0.05), model.Tuning.EnergyCostMoving);
    }

    protected override void OnCancel() => _target = null;
}
public class ShareFoodCommand : EntityCommand
{
    private double _holdEnd;

    public ShareFoodCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var victim = model.Caste == Caste.Nurse ? model.FindNearbyJuvenile(r.EatRange * 2f) ?? model.CachedNearest : model.CachedNearest;
        if (victim == null || math.distance(model.Position2D, victim.Position2D) > r.EatRange * 2f) return Fail();

        float own   = model.Stats.En / model.Stats.MaxEnergy;
        float their = victim.Stats.En / victim.Stats.MaxEnergy;
        if (their >= own * r.ShareFoodVictimRatio) return Fail();

        float portion;
        if (model.Carrying > 0f) portion = model.TakeCarried();
        else portion = model.Digestion.Withdraw(model.Digestion.Capacity * r.ShareFoodTransferFraction);
        if (portion <= 0f) return Fail();

        float rest = portion - victim.Digestion.Ingest(portion);
        if (rest > 0f) model.Digestion.Ingest(rest);
        model.Infection?.TryTransmitGift(model, victim);

        _holdEnd = HoldUntil(r.ShareFoodDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete() : EntityCommandStatus.Running;
}

public class AttackCommand : EntityCommand
{
    private enum Phase { Approach, Dash, Recover }

    private static readonly EntityModel[] Allies = new EntityModel[16];
    private EntityModel _target;
    private Phase  _phase;
    private double _recoverEnd;

    public AttackCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.Stats.En >= math.max(model.Tuning.AttackEnergyCost, SimulationRules.Active.AttackEnergyMin);

    protected override EntityCommandStatus OnBegin()
    {
        _target = model.CachedHuntTarget;
        if (_target == null || !CanExecute()) return Fail();

        var r = SimulationRules.Active;
        _phase = Phase.Approach;
        if (DistanceToTarget() <= r.DashDistance) return BeginDash(r);

        StartMove(_target.Motor.Position(), model.Tuning.MoveSpeed, r.AttackMoveMaxDuration,
            model.Tuning.EnergyCostMoving);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        var r = SimulationRules.Active;

        if (_phase == Phase.Recover)
            return Elapsed(_recoverEnd) ? Complete() : EntityCommandStatus.Running;

        if (IsGone(_target)) return Fail();

        if (_phase == Phase.Approach)
        {
            if (DistanceToTarget() <= r.DashDistance)
            {
                model.Motor.Stop();
                return BeginDash(r);
            }
            if (!TryFinishMove(out _, out bool cutApproach)) return EntityCommandStatus.Running;
            if (cutApproach) return Interrupted();
            return DistanceToTarget() <= r.DashDistance ? BeginDash(r) : Fail();
        }

        if (!TryFinishMove(out _, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();
        return Strike(r);
    }

    private EntityCommandStatus BeginDash(SimulationRules r)
    {
        var stats = model.Stats;
        if (stats.En < math.max(model.Tuning.AttackEnergyCost, r.AttackEnergyMin)) return Fail();

        stats.En = math.max(0f, stats.En - r.DashEnergyCost);
        _phase = Phase.Dash;

        float speed = model.Tuning.RunSpeed * r.DashMul;
        StartMove(_target.Motor.Position(), speed, r.DashDistance / math.max(speed * model.SpeedMul, 0.1f) + r.DashRecovery,
            model.Tuning.EnergyCostRunning);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus Strike(SimulationRules r)
    {
        var target = _target;
        if (IsGone(target)) return Fail();

        float range = model.Tuning.AttackRange;
        var stats = model.Stats;
        float cost = model.Tuning.AttackEnergyCost;
        if (stats.En < cost) return Fail();
        if (DistanceToTarget() > range || !model.TryStartAttackCooldown()) return Fail();

        float energyScale = math.saturate(stats.En / stats.MaxEnergy);
        stats.En -= cost;

        float damage = model.Tuning.AttackDamage * model.AttackDamageMul * energyScale;
        if (model.SinceAction(EntityAction.Threaten) < r.SynergyWindow) damage *= r.ThreatenAttackMul;
        damage *= 1f + r.GroupAttackBonus * CountAllies(target, r);

        target.TakeDamage(damage, model);
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.DamageDealt, damage);
        model.Infection?.TryTransmitBite(model, target);

        _phase      = Phase.Recover;
        _recoverEnd = HoldUntil(r.DashRecovery);
        model.Motor.Stop();
        return Elapsed(_recoverEnd) ? Complete() : EntityCommandStatus.Running;
    }

    private float DistanceToTarget()
        => math.distance((float3)model.Motor.Position(), (float3)_target.Motor.Position());

    private int CountAllies(EntityModel target, SimulationRules r)
    {
        int found = target.Perception.FindEntitiesInRadius(target.Motor.Position(), target, r.GroupAttackRadius, Allies);
        int n = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Allies[i];
            Allies[i] = null;
            if (ReferenceEquals(e, model) || e.SinceAction(EntityAction.Attack) > r.GroupAttackWindow) continue;
            n++;
        }
        return math.min(n, r.GroupAttackMaxCount);
    }

    protected override void OnCancel() => _target = null;
}

public class FollowCommand : PlannedMoveCommand
{
    public FollowCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    public override bool CanExecute() => model.Parent.TryGet(out _);

    protected override EntityCommandStatus OnBegin()
    {
        if (!model.Parent.TryGet(out var parent)) return Fail();

        float2 self   = model.Position2D;
        float2 target = parent.Position2D;
        if (math.distance(self, target) <= SimulationRules.Active.TetherRadius) return Complete();

        return BeginPlanned(MoveIntent.GoToPOI, target - self, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.RunSpeed, model.Tuning.EnergyCostRunning);
    }
}

public class MoveNestCommand : EntityCommand
{
    public MoveNestCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.IsLeader && model.Colony != null && !model.Colony.Nest.Contains(model.Position2D);

    protected override EntityCommandStatus OnBegin()
    {
        if (!CanExecute()) return Fail();
        model.Colony.Relocate(model.Position2D, model.FoodIndex);
        return Complete();
    }
}
