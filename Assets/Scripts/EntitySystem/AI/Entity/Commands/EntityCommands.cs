using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;

public class RestCommand : EntityCommand
{
    private float  _startHealth;
    private double _prev;

    public RestCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.Motor.Stop();
        _startHealth = model.Stats.Hp;
        _prev        = SimulationClock.TimeD;
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
            float budget = cost > 0f ? en / cost : float.MaxValue;
            float heal   = math.min(math.min(r.RestHealRate * step, stats.MaxHealth - hp), budget);
            if (heal > 0f)
            {
                stats.Hp = hp + heal;
                stats.En = math.max(0f, en - heal * cost);
            }
        }

        if (t < end) return EntityCommandStatus.Running;

        float maxHealth     = stats.MaxHealth;
        float invMax        = maxHealth > 0f ? 1f / maxHealth : 0f;
        float deficitBefore = 1f - _startHealth * invMax;
        if (deficitBefore <= r.RestDeficitThreshold) return Complete(r.RestRewardWasted);

        float recovered = math.max(0f, stats.Hp - _startHealth) * invMax;
        return Complete(r.RestRewardNeeded * math.saturate(recovered / deficitBefore));
    }
}

public class IdleCommand : EntityCommand
{
    private float _startEnergy;
    private bool  _needy;
    private bool  _overactive;

    public IdleCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.Motor.Stop();
        var r     = SimulationRules.Active;
        var stats = model.Stats;

        float energyRatio = stats.En / stats.MaxEnergy;
        float healthRatio = stats.Hp / stats.MaxHealth;

        _startEnergy = stats.En;
        _needy       = energyRatio < r.IdleLowEnergyThreshold || healthRatio < r.IdleLowEnergyThreshold;
        _overactive  = !_needy && energyRatio > r.IdleLongActivityPenaltyStart && healthRatio > r.IdleLongActivityPenaltyStart;
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!Elapsed(decision.EndTime)) return EntityCommandStatus.Running;

        var r = SimulationRules.Active;
        if (_overactive) return Complete(r.IdleRewardOveractive);
        if (!_needy)     return Complete(0f);

        float gain = (model.Stats.En - _startEnergy) / math.max(model.Stats.MaxEnergy, 1e-3f);
        return Complete(r.IdleRewardLowEnergy * math.saturate(gain / math.max(r.IdleRewardGainNorm, 1e-4f)));
    }
}

public class FleeCommand : EntityCommand
{
    private EntityModel _target;

    public FleeCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.DialogHost.UpdateDialogPosition((IDialogListener)model.Motor);

        var r = SimulationRules.Active;
        _target = model.CachedNearest;
        if (_target == null) return Complete(r.FleeRewardNoTarget);

        Vector3 self     = model.Motor.Position();
        float2  selfFlat = Extension.Flat(self);
        float2  away     = math.normalizesafe(selfFlat - Extension.Flat(_target.Motor.Position()), new float2(1f, 0f));
        float2  dest     = selfFlat + away * (model.Tuning.DetectionRadius * r.FleeDistanceMul);

        StartMove(Extension.ToWorld(dest, self.y), model.Tuning.RunSpeed, r.FleeMaxDuration, model.Tuning.EnergyCostRunning);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!TryFinishMove(out var move, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();

        var target = _target;
        _target = null;
        if (IsGone(target)) return Complete(SimulationRules.Active.FleeRewardTargetGone);

        float dist = Extension.FlatDistance(model.Motor.Position(), target.Motor.Position());
        return Complete(math.saturate(dist / model.Tuning.DetectionRadius) - PathCostPenalty(in move));
    }

    protected override void OnCancel() => _target = null;
}

public class EatCommand : EntityCommand
{
    public EatCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r     = SimulationRules.Active;
        var index = model.FoodIndex;

        if (index == null || !model.CachedFoodValid) return Complete(r.EatRewardNoFood);
        if (model.CachedFoodDistance > r.EatRange)   return Complete(FailureReward);

        bool claimed = index.TryConsumeFood(model.CachedFoodCell);
        model.InvalidateCachedFood();
        if (!claimed) return Complete(r.EatRewardNoFood);

        var stats = model.Stats;
        float heal = r.EatHealFraction * stats.MaxHealth;
        if (heal > 0f) stats.Hp = math.min(stats.Hp + heal, stats.MaxHealth);
        stats.En = math.min(stats.En + r.EatEnergyFood, stats.MaxEnergy);
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
        return Complete(r.EatRewardFood);
    }
}

public class RememberPointCommand : EntityCommand
{
    public RememberPointCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        float2 pos = Extension.Flat(model.Motor.Position());
        return Complete(model.Points.TryRemember(in pos, r.RememberPointMinSpacing)
            ? r.RememberPointRewardNew : r.RememberPointRewardDup);
    }
}

public class GoToPointCommand : EntityCommand
{
    public GoToPointCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        if (model.Points.Count == 0) return Complete(r.GoToPointRewardNoPoints);

        float2 target = model.Points.GetRandom();
        StartMove(Extension.ToWorld(in target, model.Motor.Position().y),
            model.Tuning.MoveSpeed, r.GoToPointMaxDuration, model.Tuning.EnergyCostMoving);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!TryFinishMove(out var move, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();
        return Complete(SimulationRules.Active.GoToPointRewardArrived - PathCostPenalty(in move));
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
        => Elapsed(_holdEnd) ? Complete(0.8f) : EntityCommandStatus.Running;
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
        string hash = model.Vectorizer.HashFloatArray(model.DecisionInput);
        model.Speech.SetOwnSpeech(hash);
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
        if (_failed)  return Fail();

        var r     = SimulationRules.Active;
        var stats = model.Stats;
        stats.En = math.max(0f, stats.En - r.SpeechEnergyCost);
        return Complete(r.SpeechReward);
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
        var r = SimulationRules.Active;
        var other = model.CachedNearest;
        if (other == null) return Complete(r.MimicRewardNoTarget);

        model.SetMimickedAction(other.LastActionIndex);
        return Complete(r.MimicRewardBase + other.Brain.Context.CoordMLP.AverageEntropy * r.MimicRewardEntropyScale);
    }
}

public class ThreatenCommand : EntityCommand
{
    private double _holdEnd;
    private float  _reward;

    public ThreatenCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var   target = model.CachedNearest;
        float dist   = model.CachedNearestDistance;
        float range  = model.Tuning.AttackRange;
        if (target == null) return Complete(r.ThreatenRewardNoTarget);
        if (dist > range * r.ThreatenRangeMul) return Complete(r.ThreatenRewardTooFar);

        var stats = model.Stats;
        stats.En = math.max(0f, stats.En - r.ThreatenEnergyCost);
        _reward  = dist < range ? r.ThreatenRewardClose : r.ThreatenRewardFar;
        _holdEnd = HoldUntil(r.ThreatenDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete(_reward) : EntityCommandStatus.Running;
}

public class ShareFoodCommand : EntityCommand
{
    private double _holdEnd;
    private float  _reward;

    public ShareFoodCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r     = SimulationRules.Active;
        var stats = model.Stats;
        float hp    = stats.Hp;
        float maxHp = stats.MaxHealth;
        if (hp < maxHp * r.ShareFoodMinHealthFraction) return Complete(0.1f);

        var victim = model.CachedNearest;
        if (victim == null || victim.Stats.Hp > hp * r.ShareFoodVictimRatio)
            return Complete(0.25f);

        float transfer = math.min(maxHp * r.ShareFoodTransferFraction, hp - maxHp * r.ShareFoodKeepHealthFraction);
        if (transfer <= 0f) return Complete(0.1f);
        stats.Hp = hp - transfer;

        var vs = victim.Stats;
        vs.Hp = math.min(vs.Hp + transfer, vs.MaxHealth);

        float needFactor = 1f - math.saturate(vs.Hp / vs.MaxHealth);
        _reward  = 0.5f + needFactor * 0.35f;
        _holdEnd = HoldUntil(r.ShareFoodDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete(_reward) : EntityCommandStatus.Running;
}

public class AttackCommand : EntityCommand
{
    private EntityModel _target;

    public AttackCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => model.Stats.En >= model.Tuning.AttackEnergyCost;

    protected override EntityCommandStatus OnBegin()
    {
        _target = model.CachedNearest;
        if (_target == null) return Complete(0.2f);

        StartMove(_target.Motor.Position(), model.Tuning.MoveSpeed, 2f, model.Tuning.EnergyCostMoving);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!TryFinishMove(out var move, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();

        float pathPenalty = PathCostPenalty(in move);

        var target = _target;
        _target = null;
        if (IsGone(target)) return Complete(0.3f - pathPenalty);

        float range = model.Tuning.AttackRange;
        float3 a = model.Motor.Position();
        float3 b = target.Motor.Position();

        var stats = model.Stats;
        float cost = model.Tuning.AttackEnergyCost;
        if (stats.En < cost) return Complete(FailureReward - pathPenalty);

        if (math.distancesq(a, b) <= range * range && model.TryStartAttackCooldown())
        {
            stats.En -= cost;
            float damage = model.Tuning.AttackDamage;
            target.Stats.Hp -= damage;
            model.RegisterFitnessEvent(EntityModel.FitnessEvent.DamageDealt, damage);
            return Complete(0.9f - pathPenalty);
        }

        return Complete(0.3f - pathPenalty);
    }

    protected override void OnCancel() => _target = null;
}

public class WanderCommand : EntityCommand
{
    private float2 _start;
    private float  _startEnergy;

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

        Vector3 startPos = model.Motor.Position();
        _start       = Extension.Flat(startPos);
        _startEnergy = model.Stats.En;

        float2 dest      = _start + dir * model.Tuning.WanderRadius;
        float  remaining = math.max(0f, decision.EndTime - SimulationClock.Time);

        StartMove(Extension.ToWorld(in dest, startPos.y), model.Tuning.MoveSpeed,
            remaining, model.Tuning.EnergyCostMoving);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!TryFinishMove(out var move, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();

        var r = SimulationRules.Active;
        float radius    = model.Tuning.WanderRadius;
        float travelled = math.distance(_start, Extension.Flat(model.Motor.Position()));
        float progress  = math.saturate(travelled / math.max(radius, 1e-3f));
        float spent     = math.max(0f, _startEnergy - model.Stats.En);

        return Complete(math.clamp(progress * r.WanderRewardScale
                                 - spent * r.WanderEnergyPenalty
                                 - PathCostPenalty(in move),
            r.WanderRewardMin, r.WanderRewardMax));
    }
}