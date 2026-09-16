using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;

public class RestCommand : EntityCommand
{
    public RestCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        model.Motor.Stop();
        var r = SimulationRules.Active;

        float startEnergy = model.Stats.Energy.Value;
        float startHealth = model.Stats.Health.Value;
        float start = SimulationClock.Time;
        float prev  = start;

        while (SimulationClock.Time - start < r.RestDuration)
        {
            ct.ThrowIfCancellationRequested();
            float now  = SimulationClock.Time;
            float step = now - prev;
            prev = now;

            model.Stats.Health.Value = Mathf.Min(model.Stats.Health.Value + r.RestHealRate * step, model.Stats.MaxHealth);
            model.Stats.Energy.Value = Mathf.Min(model.Stats.Energy.Value + r.RestEnergyRate * step, model.Stats.MaxEnergy);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        float deficitBefore = (1f - startEnergy / model.Stats.MaxEnergy)
                            + (1f - startHealth / model.Stats.MaxHealth);
        return deficitBefore > r.RestDeficitThreshold ? r.RestRewardNeeded : r.RestRewardWasted;
    }
}

public class IdleCommand : EntityCommand
{
    public IdleCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        model.Motor.Stop();
        var r = SimulationRules.Active;

        float energyRatio = model.Stats.Energy.Value / model.Stats.MaxEnergy;
        float healthRatio = model.Stats.Health.Value / model.Stats.MaxHealth;

        float reward;
        if (energyRatio < r.IdleLowEnergyThreshold || healthRatio < r.IdleLowEnergyThreshold)
            reward = r.IdleRewardLowEnergy;
        else if (energyRatio > r.IdleLongActivityPenaltyStart && healthRatio > r.IdleLongActivityPenaltyStart)
            reward = r.IdleRewardOveractive;
        else
            reward = 0f;

        float end = decision.EndTime;
        while (SimulationClock.Time < end)
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

        return reward;
    }
}
public class FleeCommand : EntityCommand
{
    public FleeCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        model.DialogHost.UpdateDialogPosition((IDialogListener)model.Motor);

        var target = model.Perception.FindNearestEntity(model.Motor.Position(), model.SelfObject, out _);
        if (target == null) return 0.3f;

        Vector3 self = model.Motor.Position();
        float2 selfFlat = Extension.Flat(self);
        float2 delta = selfFlat - Extension.Flat(target.transform.position);
        float2 away = math.normalizesafe(delta, new float2(1f, 0f));
        float2 dest = selfFlat + away * (model.Tuning.DetectionRadius * 1.5f);

        await model.Motor.MoveToAsync(Extension.ToWorld(dest, self.y),
            model.Tuning.RunSpeed, 2f, model.Tuning.EnergyCostRunning, ct);

        if (target == null) return 0.5f;

        float dist = Extension.FlatDistance(model.Motor.Position(), target.transform.position);
        return math.saturate(dist / model.Tuning.DetectionRadius);
    }
}

public class EatCommand : EntityCommand
{
    public EatCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        var r = SimulationRules.Active;

        bool claimed = false;
        var index = model.FoodIndex;

        if (index != null &&
            index.TryFindNearestFood(
                model.Motor.Position().x,
                model.Motor.Position().z,
                model.Tuning.DetectionRadius,
                out long cell, out _))
        {
            claimed = index.TryConsumeFood(cell);
        }

        float reward;
        if (claimed)
        {
            model.Stats.Health.Value = Mathf.Min(model.Stats.Health.Value + r.EatHealFood, model.Stats.MaxHealth);
            model.Stats.Energy.Value = Mathf.Min(model.Stats.Energy.Value + r.EatEnergyFood, model.Stats.MaxEnergy);
            model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
            reward = r.EatRewardFood;
        }
        else
        {
            model.Stats.Health.Value = Mathf.Min(model.Stats.Health.Value + r.EatHealNoFood, model.Stats.MaxHealth);
            model.Stats.Energy.Value = Mathf.Min(model.Stats.Energy.Value + r.EatEnergyNoFood, model.Stats.MaxEnergy);
            reward = r.EatRewardNoFood;
        }

        await UniTask.Yield(ct);
        return reward;
    }
}

public class RememberPointCommand : EntityCommand
{
    public RememberPointCommand(EntityModel model) : base(model) { }

    protected override UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        float2 pos = Extension.Flat(model.Motor.Position());
        bool added = model.Points.TryRemember(in pos, 10f);
        return UniTask.FromResult(added ? 0.65f : 0.3f);
    }
}

public class GoToPointCommand : EntityCommand
{
    public GoToPointCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        if (model.Points.Count == 0) return 0f;

        float2 target = model.Points.GetRandom();
        await model.Motor.MoveToAsync(Extension.ToWorld(in target, model.Motor.Position().y),
            model.Tuning.MoveSpeed, 5f, model.Tuning.EnergyCostMoving, ct);

        return 0.55f;
    }
}

public class ReproduceCommand : EntityCommand
{
    public ReproduceCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() =>
        model.Stats.Energy.Value >= model.Tuning.ReproduceEnergyCost &&
        model.Stats.Health.Value >= model.Tuning.ReproduceHealthCost;

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        model.Stats.Energy.Value -= model.Tuning.ReproduceEnergyCost;
        model.Stats.Health.Value -= model.Tuning.ReproduceHealthCost;
        model.RequestReproduce();
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.Reproduced);

        await UniTask.Delay((int)(model.Tuning.IdleTime * 1000), cancellationToken: ct);
        return 0.8f;
    }
}

public class SpeechCommand : EntityCommand
{
    public SpeechCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        string hash = model.Vectorizer.HashFloatArray(model.LastInput);
        model.Speech.SetOwnSpeech(hash);
        DialogSystem.Instance.OnSpeechSend((IDialogSender)model.Motor, hash);

        var nearest = model.Perception.FindNearestEntity(model.Motor.Position(), model.SelfObject, out float dist);
        await model.Speech.PlayAsync(
            hash, model.Stats.Health.Value, model.Stats.Energy.Value,
            0f, 0f, model.LastActionIndex, dist, ct);

        model.Stats.Energy.Value -= 5f;
        return 0.55f;
    }
}

public class MimicCommand : EntityCommand
{
    public MimicCommand(EntityModel model) : base(model) { }

    protected override UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        var target = model.Perception.FindNearestEntity(model.Motor.Position(), model.SelfObject, out _);
        var otherModel = target?.GetComponent<EntityViewModel>()?.Entity as EntityModel;
        if (otherModel == null || otherModel.IsDisposed)
            return UniTask.FromResult(0.2f);

        model.SetMimickedAction(otherModel.LastActionIndex);
        float reward = 0.3f + otherModel.Brain.Context.CoordMLP.AverageEntropy * 0.2f;
        return UniTask.FromResult(reward);
    }
}

public class ThreatenCommand : EntityCommand
{
    public ThreatenCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        var target = model.Perception.FindNearestEntity(model.Motor.Position(), model.SelfObject, out float dist);
        if (target == null || dist > model.Tuning.AttackRange * 2f)
            return target == null ? 0.2f : 0.15f;

        model.Stats.Energy.Value -= 3f;
        await UniTask.Delay(800, cancellationToken: ct);
        return dist < model.Tuning.AttackRange ? 0.6f : 0.4f;
    }
}

public class ShareFoodCommand : EntityCommand
{
    public ShareFoodCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        if (model.Stats.Health.Value < 80f) return 0.1f;

        var target = model.Perception.FindNearestEntity(model.Motor.Position(), model.SelfObject, out _);
        var victim = target != null ? target.GetComponent<EntityViewModel>()?.Entity as EntityModel : null;
        if (victim == null || victim.IsDisposed || victim.Stats.Health.Value > model.Stats.Health.Value * 0.8f)
            return 0.25f;

        float transfer = Mathf.Min(20f, model.Stats.Health.Value - 60f);
        model.Stats.Health.Value -= transfer;
        victim.Stats.Health.Value = Mathf.Min(victim.Stats.Health.Value + transfer, victim.Stats.MaxHealth);

        float needFactor = 1f - Mathf.Clamp01(victim.Stats.Health.Value / victim.Stats.MaxHealth);
        await UniTask.Delay(500, cancellationToken: ct);
        return 0.5f + needFactor * 0.35f;
    }
}

public class AttackCommand : EntityCommand
{
    public AttackCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => model.Stats.Energy.Value >= model.Tuning.AttackEnergyCost;

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        var target = model.Perception.FindNearestEntity(model.Motor.Position(), model.SelfObject, out _);
        if (target == null) return 0.2f;

        Vector3 targetPos = target.transform.position;
        await model.Motor.MoveToAsync(targetPos, model.Tuning.MoveSpeed, 2f,
            model.Tuning.EnergyCostMoving, ct);

        if (target == null) return 0.3f;

        if (Vector3.Distance(model.Motor.Position(), target.transform.position) <= model.Tuning.AttackRange
            && model.TryStartAttackCooldown())
        {
            var victim = target.GetComponent<EntityViewModel>()?.Entity as EntityModel;
            if (victim != null && !victim.IsDisposed)
            {
                victim.Stats.Health.Value -= model.Tuning.AttackDamage;
                model.RegisterFitnessEvent(EntityModel.FitnessEvent.DamageDealt, model.Tuning.AttackDamage);
            }
            return victim != null ? 0.9f : 0.4f;
        }

        return 0.3f;
    }
}

public class WanderCommand : EntityCommand
{
    public WanderCommand(EntityModel model) : base(model) { }

    protected override async UniTask<float> RunAsync(BrainDecision decision, CancellationToken ct)
    {
        var r = SimulationRules.Active;

        var randomCircle = RavineRandom.GetInsideCircle();
        float2 dir = math.normalizesafe(new float2(randomCircle.x, randomCircle.y), new float2(1f, 0f));

        Vector3 startPos = model.Motor.Position();
        float2 start = Extension.Flat(startPos);
        float startEnergy = model.Stats.Energy.Value;

        float radius = model.Tuning.WanderRadius;
        float2 dest = start + dir * radius;

        await model.Motor.MoveToAsync(Extension.ToWorld(in dest, startPos.y), model.Tuning.MoveSpeed,
            RavineRandom.RangeFloat(model.Tuning.MinWanderTime, model.Tuning.MaxWanderTime),
            model.Tuning.EnergyCostMoving, ct);

        float travelled = math.distance(start, Extension.Flat(model.Motor.Position()));
        float progress = math.saturate(travelled / math.max(radius, 1e-3f));
        float spent = math.max(0f, startEnergy - model.Stats.Energy.Value);

        return math.clamp(progress * r.WanderRewardScale - spent * r.WanderEnergyPenalty,
            r.WanderRewardMin, r.WanderRewardMax);
    }
}