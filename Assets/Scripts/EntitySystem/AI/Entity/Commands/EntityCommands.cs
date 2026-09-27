using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;
using TheRavine.EntityControl.Virology;

public class RestCommand : EntityCommand
{
    private static readonly EntityModel[] Nearby = new EntityModel[16];
    private double _prev;
    private float  _rested;

    public RestCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        if (model.IsHungry) return Fail(SimulationRules.Active.InfeasibleActionReward);
        model.Motor.Stop();
        _prev   = SimulationClock.TimeD;
        _rested = 0f;
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        var r     = SimulationRules.Active;
        var stats = model.Stats;
        if (model.IsHungry) return Fail(r.InfeasibleActionReward);

        double end = decision.EndTime;
        double t   = math.min(SimulationClock.TimeD, end);
        float step = (float)(t - _prev);
        if (step > 0f)
        {
            _prev = t;
            _rested += step;
            float hp     = stats.Hp;
            float en     = stats.En;
            float cost   = r.RestHealEnergyCost;
            var   nest   = model.Nest;
            float aura   = model.IsAtNest && nest != null
                ? 1f + r.NurseHealAura * math.min(nest.NursesAtNest, r.NurseHealAuraMax)
                : 1f;
            float rate   = r.RestHealRate * (model.IsAtNest ? r.NestRestHealMul : 1f) * model.RestHealMul * aura;
            float spare  = math.max(0f, en - r.StarvationThreshold - r.RestMinEnergyReserve);
            float budget = cost > 0f ? spare / cost : (spare > 0f ? float.MaxValue : 0f);
            float heal   = math.min(math.min(rate * step, stats.MaxHealth - hp), budget);
            if (heal > 0f)
            {
                stats.Hp = hp + heal;
                stats.En = math.max(0f, en - heal * cost);
            }
        }

        if (t < end) return EntityCommandStatus.Running;

        float factor = RestFactor(r);
        model.Brain.Context.GoalRestScore += factor;
        return Complete(r.RestRewardPerSecond * _rested * factor);
    }

    private float RestFactor(SimulationRules r)
    {
        int found = model.Perception.FindEntitiesInRadius(model.Motor.Position(), model, r.CrowdRadius, Nearby);
        int resting = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Nearby[i];
            Nearby[i] = null;
            if (e == null || e.Colony != model.Colony || !e.IsCommandRunning || e.LastAction != EntityAction.Rest) continue;
            resting++;
        }
        float social = 1f + r.RestSocialBonus * math.min(resting, r.RestSocialMax);
        return social * (model.IsNight && model.IsAtNest ? r.RestNightMul : 1f);
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
        float travel    = radius / math.max(speed * model.SpeedMul, 0.1f) * SimulationRules.Frame.LegTimeSlack;
        if (travel > remaining)
        {
            remaining = travel;
            ExtendWatchdog(SimulationClock.TimeD + travel);
        }
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
        _reached = false;

        var nest = model.Nest;
        if (decision.Plan == PlanKind.Migrate && nest != null && nest.MigrationPressure > 1e-4f)
        {
            desired = math.normalizesafe(nest.Migration);
            radius  = math.min(r.MigrateLegRadius, r.PlannerRadiusMax);
        }
        else if (decision.HasDestination)
        {
            float2 self = model.Position2D;
            float2 to   = decision.Destination - self;
            float  dist = math.length(to);
            _reached = dist <= r.PlanArriveRadius;
            if (_reached) return Complete();
            return BeginPlanned(MoveIntent.Wander, to, decision.Destination, true,
                math.min(dist, r.PlannerRadiusMax), model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
        }
        else if (decision.HasDirection && model.TryResolveDirection(decision.Direction, decision.DirOffset, out desired, out float dirRadius))
            radius = math.max(dirRadius, r.LevyMinStep);
        else if (decision.HasHeading) desired = decision.Heading;
        else if (!TryFieldGradient(r, radius, out desired))
        {
            float2 course = model.Heading;
            if (math.lengthsq(course) < 1e-6f)
            {
                float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
                math.sincos(a, out float sa, out float ca);
                course = new float2(ca, sa);
            }
            math.sincos(model.OuAngle, out float so, out float co);
            desired = new float2(course.x * co - course.y * so, course.x * so + course.y * co);
            radius  = math.min(model.LevyStep(in SimulationRules.Frame), r.PlannerRadiusMax);
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

    private bool _reached;

    protected override bool PauseAfterArrival => !decision.HasDestination || _reached;

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        if (decision.HasDestination)
            _reached = math.distance(model.Position2D, decision.Destination) <= SimulationRules.Frame.PlanArriveRadius;
        if (decision.Plan != PlanKind.Migrate || model.Caste != Caste.Scout) return Complete();
        var colony = model.Colony;
        if (colony == null || colony.IsWild) return Complete();

        ref readonly var r = ref SimulationRules.Frame;
        float2 here = model.Position2D;
        if (!colony.Nest.Contains(here) && colony.Nest.FieldAt(ColonyChannel.Danger, here) < r.QuorumMaxDanger)
            colony.VoteNestSite(here, model.EntityId);
        return Complete();
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

    public override bool CanExecute()
        => model.IsRecruited || model.Points.Count > 0 || (model.Colony != null && model.Colony.Pois.Count > 0);

    protected override EntityCommandStatus OnBegin()
    {
        float2 self = model.Position2D;
        float2 target;
        bool own;

        if (Source == CommandSource.Instinct && model.IsRecruited)
        {
            target = model.RecruitTarget;
            own    = true;
        }
        else if (decision.Direction == (byte)DirectionKind.OwnPoi)
            own = model.Points.TryPickBest(in self, out target);
        else if (decision.Direction == (byte)DirectionKind.ColonyPoi && model.Colony != null)
            own = model.Colony.Pois.TryPickBest(self, out target, out _);
        else
        {
            own = model.Points.TryPickBest(in self, out target);
            var colony = model.Colony;
            if (colony != null && colony.Pois.TryPickBest(self, out float2 shared, out _)
                && (!own || math.distancesq(shared, self) < math.distancesq(target, self)))
            {
                target = shared;
                own = true;
            }
        }
        if (!own) return Fail();

        return BeginPlanned(MoveIntent.GoToPOI, target - self, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        float2 self = model.Position2D;
        model.Points.Visit(in self);
        if (Source == CommandSource.Instinct) model.ClearRecruit();
        if (!model.CachedFoodValid) model.Points.MarkNegative(in self, SimulationRules.Frame.NegativePoiEmpty);
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

        if (!model.TryGetThreatSource(out _threat) && !TryDangerSource(r, self, total, out _threat)) return Complete();

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

        if (!foodNear && nest != null && model.CanEatFromNest && nest.Storage > 0f)
        {
            float cost = math.max(model.CasteMods.NestFoodCost, 1e-3f);
            float take = math.min(nest.Storage, r.NestEatPortion * cost);
            nest.Storage -= take;
            model.Colony?.Stats.RecordStorageEaten(take);
            return StartEating(take / cost, false);
        }

        if (!foodNear) return Fail();

        long cell = model.CachedFoodCell;
        model.InvalidateCachedFood();
        if (!index.TryClaim(cell, model, SimulationClock.TimeD, out var claim)) return Fail();
        model.NoteFoodSite(ChunkFoodIndex.CellCenter(cell));

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
        _end     = _start + math.max(r.EatSecondsPerEnergy * portion / math.max(model.CasteMods.EatSpeed, 1e-3f), 1e-3f);

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
        model.NoteFoodSite(ChunkFoodIndex.CellCenter(cell));

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
        ref readonly var r = ref SimulationRules.Frame;
        float stored = model.TakeCarried() * model.CasteMods.StoreEfficiency;
        nest.Storage += stored;
        model.RecordStored(stored);
        model.Colony?.Stats.RecordStorageStored(stored);
        model.DepositTrail();
        model.ShareMemoryWithColony();
        if (model.TryGetFoodSite(out float2 site)) model.RecruitNearby(site, r.RecruitCount, nest.Radius);
        return Complete(r.StoreFoodReward * stored / math.max(r.NestStorageNorm, 1e-3f));
    }
}

public class RememberPointCommand : EntityCommand
{
    public RememberPointCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        float2 pos = model.Position2D;
        model.Points.TryRemember(in pos, SimulationRules.Active.RememberPointMinSpacing);
        if (model.Caste == Caste.Scout)
            model.Colony?.Pois.Offer(pos, model.CachedFoodValid ? SimulationRules.Active.EatEnergyFood * model.CasteMods.PoiWeight : 0f, 0f);
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
        ApplySignal(r);

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

    private void ApplySignal(SimulationRules r)
    {
        switch (decision.Signal)
        {
            case SpeechSignal.Alarm:
                if (model.HasDirectThreat) model.Nest?.RaiseAlarm(r.AlarmCryAmount);
                break;
            case SpeechSignal.Food:
                if (model.TryGetFoodSite(out float2 site)) model.RecruitNearby(site, r.RecruitCount, r.SpeechRadius);
                break;
            case SpeechSignal.FollowMe:
                model.RecruitNearby(model.Position2D, r.RecruitCount, r.SpeechRadius);
                break;
        }
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
            (float)math.max(_holdEnd - SimulationClock.TimeD, 0.05), model.Tuning.EnergyCostMoving);
    }

    protected override void OnCancel() => _target = default;
}
public class ShareFoodCommand : EntityCommand
{
    private double _holdEnd;

    public ShareFoodCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        bool troph = Source == CommandSource.Instinct;
        var victim = troph
            ? model.FindHungriestKin(r.EatRange * 2f)
            : model.Caste == Caste.Nurse ? model.FindNearbyJuvenile(r.EatRange * 2f) ?? model.CachedNearest : model.CachedNearest;
        if (victim == null || math.distance(model.Position2D, victim.Position2D) > r.EatRange * 2f) return Fail();

        float own   = model.Stats.En / model.Stats.MaxEnergy;
        float their = victim.Stats.En / victim.Stats.MaxEnergy;
        if (their >= own * r.ShareFoodVictimRatio) return Fail();

        float portion;
        if (model.Carrying > 0f) portion = model.TakeCarried();
        else portion = model.Digestion.Withdraw(model.Digestion.Capacity * r.ShareFoodTransferFraction * model.CasteMods.Feed);
        if (portion <= 0f) return Fail();

        float given = victim.Digestion.Ingest(portion);
        float rest  = portion - given;
        if (rest > 0f) model.Digestion.Ingest(rest);
        if (troph && model.Nest != null)
            model.Nest.RelieveHunger(given / math.max(r.NestStorageNorm, 1e-3f) * r.TrophallaxisHungerRelief);
        model.Infection?.TryTransmitGift(model, victim);

        _holdEnd = HoldUntil(r.ShareFoodDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete() : EntityCommandStatus.Running;
}

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
            float dash = r.DashDistance / math.max(model.Tuning.RunSpeed * r.DashMul * model.SpeedMul, 0.1f);
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
        StartMove(_target.Position, speed, r.DashDistance / math.max(speed * model.SpeedMul, 0.1f) + r.DashRecovery,
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
        => model.IsLeader && model.Colony != null && model.Colony.TryGetQuorumSite(out float2 site)
           && !model.Colony.Nest.Contains(site);

    protected override EntityCommandStatus OnBegin()
    {
        var colony = model.Colony;
        if (colony == null || !model.IsLeader || !colony.TryGetQuorumSite(out float2 site) || colony.Nest.Contains(site))
            return Fail();
        colony.Relocate(site, model.FoodIndex);
        return Complete();
    }
}

public class GroomCommand : EntityCommand
{
    private double _holdEnd;

    public GroomCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var patient = model.FindSickestKin(r.EatRange * 2f, r.GroomMinLoad);
        if (patient == null) return Fail();

        model.Motor.Stop();
        patient.Virology?.Groom(r.GroomAmount);
        model.Infection?.TryTransmitContact(patient, model, r.GroomInfectChance);
        _holdEnd = HoldUntil(model.Tuning.IdleTime);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete() : EntityCommandStatus.Running;
}
