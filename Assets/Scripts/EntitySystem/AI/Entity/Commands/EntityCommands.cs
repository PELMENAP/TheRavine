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
        _startHealth = model.Stats.Health.Value;
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
            stats.Health.Value = math.min(stats.Health.Value + r.RestHealRate * step, stats.MaxHealth);
        }

        if (t < end) return EntityCommandStatus.Running;

        float maxHealth     = stats.MaxHealth;
        float invMax        = maxHealth > 0f ? 1f / maxHealth : 0f;
        float deficitBefore = 1f - _startHealth * invMax;
        if (deficitBefore <= r.RestDeficitThreshold) return Complete(r.RestRewardWasted);

        float recovered = math.max(0f, stats.Health.Value - _startHealth) * invMax;
        return Complete(r.RestRewardNeeded * math.saturate(recovered / deficitBefore));
    }
}

public class IdleCommand : EntityCommand
{
    private float _reward;

    public IdleCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.Motor.Stop();
        var r = SimulationRules.Active;

        float energyRatio = model.Stats.Energy.Value / model.Stats.MaxEnergy;
        float healthRatio = model.Stats.Health.Value / model.Stats.MaxHealth;

        if (energyRatio < r.IdleLowEnergyThreshold || healthRatio < r.IdleLowEnergyThreshold)
            _reward = r.IdleRewardLowEnergy;
        else if (energyRatio > r.IdleLongActivityPenaltyStart && healthRatio > r.IdleLongActivityPenaltyStart)
            _reward = r.IdleRewardOveractive;
        else
            _reward = 0f;

        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(decision.EndTime) ? Complete(_reward) : EntityCommandStatus.Running;
}

public class FleeCommand : EntityCommand
{
    private EntityModel _target;

    public FleeCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.DialogHost.UpdateDialogPosition((IDialogListener)model.Motor);

        _target = model.CachedNearest;
        if (_target == null) return Complete(0.3f);

        Vector3 self     = model.Motor.Position();
        float2  selfFlat = Extension.Flat(self);
        float2  away     = math.normalizesafe(selfFlat - Extension.Flat(_target.Motor.Position()), new float2(1f, 0f));
        float2  dest     = selfFlat + away * (model.Tuning.DetectionRadius * 1.5f);

        StartMove(Extension.ToWorld(dest, self.y), model.Tuning.RunSpeed, 2f, model.Tuning.EnergyCostRunning);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!TryFinishMove(out var move, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();

        var target = _target;
        _target = null;
        if (IsGone(target)) return Complete(0.5f);

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
        stats.Health.Value = math.min(stats.Health.Value + r.EatHealFood,   stats.MaxHealth);
        stats.Energy.Value = math.min(stats.Energy.Value + r.EatEnergyFood, stats.MaxEnergy);
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
        return Complete(r.EatRewardFood);
    }
}

public class RememberPointCommand : EntityCommand
{
    public RememberPointCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        float2 pos = Extension.Flat(model.Motor.Position());
        return Complete(model.Points.TryRemember(in pos, 10f) ? 0.65f : 0.3f);
    }
}

public class GoToPointCommand : EntityCommand
{
    public GoToPointCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        if (model.Points.Count == 0) return Complete(0f);

        float2 target = model.Points.GetRandom();
        StartMove(Extension.ToWorld(in target, model.Motor.Position().y),
            model.Tuning.MoveSpeed, 5f, model.Tuning.EnergyCostMoving);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!TryFinishMove(out var move, out bool cut)) return EntityCommandStatus.Running;
        if (cut) return Interrupted();
        return Complete(0.55f - PathCostPenalty(in move));
    }
}

public class ReproduceCommand : EntityCommand
{
    private double _holdEnd;

    public ReproduceCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() =>
        model.Stats.Energy.Value >= model.Tuning.ReproduceEnergyCost &&
        model.Stats.Health.Value >= model.Tuning.ReproduceHealthCost;

    protected override EntityCommandStatus OnBegin()
    {
        model.Stats.Energy.Value -= model.Tuning.ReproduceEnergyCost;
        model.Stats.Health.Value -= model.Tuning.ReproduceHealthCost;
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

        model.Stats.Energy.Value -= 5f;
        return Complete(0.55f);
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
                hash, model.Stats.Health.Value, model.Stats.Energy.Value,
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
        if (other == null) return Complete(0.2f);

        model.SetMimickedAction(other.LastActionIndex);
        return Complete(0.3f + other.Brain.Context.CoordMLP.AverageEntropy * 0.2f);
    }
}

public class ThreatenCommand : EntityCommand
{
    private double _holdEnd;
    private float  _reward;

    public ThreatenCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var   target = model.CachedNearest;
        float dist   = model.CachedNearestDistance;
        if (target == null) return Complete(0.2f);
        if (dist > model.Tuning.AttackRange * 2f) return Complete(0.15f);

        model.Stats.Energy.Value -= 3f;
        _reward  = dist < model.Tuning.AttackRange ? 0.6f : 0.4f;
        _holdEnd = HoldUntil(SimulationRules.Active.ThreatenDuration);
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
        var stats = model.Stats;
        if (stats.Health.Value < 80f) return Complete(0.1f);

        var victim = model.CachedNearest;
        if (victim == null || victim.Stats.Health.Value > stats.Health.Value * 0.8f)
            return Complete(0.25f);

        float transfer = math.min(20f, stats.Health.Value - 60f);
        stats.Health.Value -= transfer;

        var vs = victim.Stats;
        vs.Health.Value = math.min(vs.Health.Value + transfer, vs.MaxHealth);

        float needFactor = 1f - math.saturate(vs.Health.Value / vs.MaxHealth);
        _reward  = 0.5f + needFactor * 0.35f;
        _holdEnd = HoldUntil(SimulationRules.Active.ShareFoodDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete(_reward) : EntityCommandStatus.Running;
}

public class AttackCommand : EntityCommand
{
    private EntityModel _target;

    public AttackCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => model.Stats.Energy.Value >= model.Tuning.AttackEnergyCost;

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

        if (math.distancesq(a, b) <= range * range && model.TryStartAttackCooldown())
        {
            float damage = model.Tuning.AttackDamage;
            target.Stats.Health.Value -= damage;
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
        _startEnergy = model.Stats.Energy.Value;

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
        float spent     = math.max(0f, _startEnergy - model.Stats.Energy.Value);

        return Complete(math.clamp(progress * r.WanderRewardScale
                                 - spent * r.WanderEnergyPenalty
                                 - PathCostPenalty(in move),
            r.WanderRewardMin, r.WanderRewardMax));
    }
}