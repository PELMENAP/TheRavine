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
            float rate   = r.RestHealRate * (model.IsAtNest ? r.NestRestHealMul : 1f);
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
        float radius, float speed, float energyCost, float2 threat = default, bool hasThreat = false)
    {
        _pausing = false;
        float remaining = math.max(0f, decision.EndTime - SimulationClock.Time);
        StartPlannedMove(intent, direction, target, hasTarget, radius, speed, remaining, energyCost, threat, hasThreat);
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
        float2 desired;
        if (decision.HasHeading) desired = decision.Heading;
        else
        {
            var c = RavineRandom.GetInsideCircle();
            desired = new float2(c.x, c.y);
        }

        float2 dir = TerrainSteering.Blend(in desired, in model.LastTerrain);
        return BeginPlanned(MoveIntent.Wander, dir, default, false,
            model.Tuning.WanderRadius, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
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

    public override bool CanExecute() => model.Points.Count > 0;

    protected override EntityCommandStatus OnBegin()
    {
        if (model.Points.Count == 0) return Fail();

        float2 target = model.Points.GetRandom();
        return BeginPlanned(MoveIntent.GoToPOI, target - model.Position2D, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }
}

public class FleeCommand : PlannedMoveCommand
{
    public FleeCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    protected override EntityCommandStatus OnBegin()
    {
        model.DialogHost.UpdateDialogPosition((IDialogListener)model.Motor);

        var r = SimulationRules.Active;
        var threat = model.CachedNearest;
        if (threat == null) return Complete();

        float2 self  = model.Position2D;
        float2 enemy = Extension.Flat(threat.Motor.Position());
        float  speed = model.Tuning.RunSpeed * (model.IsWarned ? r.WarnedFleeSpeedMul : 1f);

        return BeginPlanned(MoveIntent.Flee, self - enemy, default, false,
            model.Tuning.DetectionRadius * r.FleeDistanceMul, speed, model.Tuning.EnergyCostRunning, enemy, true);
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

    public override bool CanExecute() => model.Carrying <= 0f;

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
    private double _holdEnd;

    public ThreatenCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var   target = model.CachedNearest;
        float dist   = model.CachedNearestDistance;
        if (target == null || dist > model.Tuning.AttackRange * r.ThreatenRangeMul) return Fail();

        var stats = model.Stats;
        stats.En = math.max(0f, stats.En - r.ThreatenEnergyCost);
        _holdEnd = HoldUntil(r.ThreatenDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete() : EntityCommandStatus.Running;
}

public class ShareFoodCommand : EntityCommand
{
    private double _holdEnd;

    public ShareFoodCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var victim = model.CachedNearest;
        if (victim == null || model.CachedNearestDistance > r.EatRange * 2f) return Fail();

        float own   = model.Stats.En / model.Stats.MaxEnergy;
        float their = victim.Stats.En / victim.Stats.MaxEnergy;
        if (their >= own * r.ShareFoodVictimRatio) return Fail();

        float portion;
        if (model.Carrying > 0f) portion = model.TakeCarried();
        else portion = model.Digestion.Withdraw(model.Digestion.Capacity * r.ShareFoodTransferFraction);
        if (portion <= 0f) return Fail();

        float rest = portion - victim.Digestion.Ingest(portion);
        if (rest > 0f) model.Digestion.Ingest(rest);

        _holdEnd = HoldUntil(r.ShareFoodDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete() : EntityCommandStatus.Running;
}

public class AttackCommand : EntityCommand
{
    private static readonly EntityModel[] Allies = new EntityModel[16];
    private EntityModel _target;

    public AttackCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => model.Stats.En >= model.Tuning.AttackEnergyCost;

    protected override EntityCommandStatus OnBegin()
    {
        _target = model.CachedNearest;
        if (_target == null) return Fail();

        StartMove(_target.Motor.Position(), model.Tuning.MoveSpeed, SimulationRules.Active.AttackMoveMaxDuration,
            model.Tuning.EnergyCostMoving);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!TryFinishMove(out _, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();

        var target = _target;
        _target = null;
        if (IsGone(target)) return Fail();

        var r = SimulationRules.Active;
        float range = model.Tuning.AttackRange;
        float3 a = model.Motor.Position();
        float3 b = target.Motor.Position();

        var stats = model.Stats;
        float cost = model.Tuning.AttackEnergyCost;
        if (stats.En < cost) return Fail();
        if (math.distancesq(a, b) > range * range || !model.TryStartAttackCooldown()) return Fail();

        stats.En -= cost;

        float damage = model.Tuning.AttackDamage * model.AttackDamageMul;
        if (model.SinceAction(EntityAction.Threaten) < r.SynergyWindow) damage *= r.ThreatenAttackMul;
        damage *= 1f + r.GroupAttackBonus * CountAllies(target, r);

        target.TakeDamage(damage, model);
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.DamageDealt, damage);
        model.Infection?.TryTransmitBite(model, target);
        return Complete();
    }

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
