using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class IdleCommand : ICommand
{
    private readonly EntityModel model;
    private const float LowEnergyThreshold = 0.35f;
    private const float LongActivityPenaltyStart = 0.85f;

    public IdleCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        model.Motor.Stop();

        float energyRatio = model.Stats.Energy.Value / model.Stats.MaxEnergy;
        float healthRatio = model.Stats.Health.Value / model.Stats.MaxHealth;

        float reward;
        if (energyRatio < LowEnergyThreshold || healthRatio < LowEnergyThreshold)
            reward = 0.6f;
        else if (energyRatio > LongActivityPenaltyStart && healthRatio > LongActivityPenaltyStart)
            reward = -0.4f;
        else
            reward = 0f;

        model.Brain.GiveReward(reward);
        await UniTask.Delay((int)(model.Tuning.IdleTime * 1000));
    }

    public void Cancel() { }
}
public class FleeCommand : ICommand
{
    private readonly EntityModel model;
    private CancellationTokenSource cts = new();
    public FleeCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        model.DialogHost.UpdateDialogPosition((IDialogListener)model.Motor);

        var target = model.Perception.FindNearestEntity(model.Motor.Position, model.SelfObject, out _);
        if (target == null) { model.Brain.GiveReward(0.3f); return; }

        Vector2 away = ((Vector2)model.Motor.Position - (Vector2)target.transform.position).normalized;
        Vector2 dest = (Vector2)model.Motor.Position + away * model.Tuning.DetectionRadius * 1.5f;

        await model.Motor.MoveToAsync(new Vector3(dest.x, model.Motor.Position.y, dest.y),
            model.Tuning.RunSpeed, 2f, model.Tuning.EnergyCostRunning, cts.Token);

        float dist = Vector2.Distance(model.Motor.Position, target.transform.position);
        model.Brain.GiveReward(Mathf.Clamp01(dist / model.Tuning.DetectionRadius));
    }

    public void Cancel() => cts.Cancel();
}

public class EatCommand : ICommand
{
    private readonly EntityModel model;
    public EatCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        var food = model.Perception.FindNearestFood(model.Motor.Position);
        if (food != null)
        {
            model.Stats.Health.Value = Mathf.Min(model.Stats.Health.Value + 30f, model.Stats.MaxHealth);
            model.Stats.Energy.Value = Mathf.Min(model.Stats.Energy.Value + 20f, model.Stats.MaxEnergy);
            model.Brain.GiveReward(0.85f);

            model.Feedback.FlashColor(Color.magenta, 0).Forget();

            Object.Destroy(food.gameObject);
        }
        else
        {
            model.Stats.Health.Value = Mathf.Min(model.Stats.Health.Value + 5f, model.Stats.MaxHealth);
            model.Stats.Energy.Value = Mathf.Min(model.Stats.Energy.Value + 5f, model.Stats.MaxEnergy);
            model.Brain.GiveReward(0.35f);
        }



        await UniTask.Yield();
    }

    public void Cancel() { }
}

public class RestCommand : ICommand
{
    private readonly EntityModel model;
    public RestCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        model.Motor.Stop();
        float startEnergy = model.Stats.Energy.Value;
        float startHealth = model.Stats.Health.Value;

        float elapsed = 0f;
        const float duration = 3f;
        while (elapsed < duration)
        {
            model.Stats.Health.Value = Mathf.Min(model.Stats.Health.Value + 5f * Time.deltaTime, model.Stats.MaxHealth);
            model.Stats.Energy.Value = Mathf.Min(model.Stats.Energy.Value + 8f * Time.deltaTime, model.Stats.MaxEnergy);
            elapsed += Time.deltaTime;
            await UniTask.Yield();
        }

        float deficitBefore = (1f - startEnergy / model.Stats.MaxEnergy) + (1f - startHealth / model.Stats.MaxHealth);
        float reward = deficitBefore > 0.3f ? 0.7f : -0.2f;
        model.Brain.GiveReward(reward);
    }

    public void Cancel() { }
}

public class RememberPointCommand : ICommand
{
    private readonly EntityModel model;
    public RememberPointCommand(EntityModel model) => this.model = model;

    public UniTask ExecuteAsync()
    {
        Vector2 pos = model.Motor.Position;
        bool added = model.Points.TryRemember(pos, 10f);
        model.Brain.GiveReward(added ? 0.65f : 0.3f);
        if (added) model.Feedback.FlashColor(Color.cyan, 0);
        return UniTask.CompletedTask;
    }

    public void Cancel() { }
}

public class GoToPointCommand : ICommand
{
    private readonly EntityModel model;
    private CancellationTokenSource cts = new();
    public GoToPointCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        if (model.Points.Count == 0) return;
        Vector2 target = model.Points.GetRandom();
        await model.Motor.MoveToAsync(new Vector3(target.x, model.Motor.Position.y, target.y),
            model.Tuning.MoveSpeed, 5f, model.Tuning.EnergyCostMoving, cts.Token);

        model.Feedback.FlashColor(Color.blue, 0).Forget();
        model.Brain.GiveReward(0.55f);
    }

    public void Cancel() => cts.Cancel();
}

public class ReproduceCommand : ICommand
{
    private readonly EntityModel model;
    public ReproduceCommand(EntityModel model) => this.model = model;

    public bool CanExecute() =>
        model.Stats.Energy.Value >= model.Tuning.ReproduceEnergyCost &&
        model.Stats.Health.Value >= model.Tuning.ReproduceHealthCost;
    public async UniTask ExecuteAsync()
    {
        model.Stats.Energy.Value -= model.Tuning.ReproduceEnergyCost;
        model.Stats.Health.Value -= model.Tuning.ReproduceHealthCost;
        model.RequestReproduce();
        model.Brain.GiveReward(0.8f);
        await UniTask.Delay((int)(model.Tuning.IdleTime * 1000));
    }

    public void Cancel() { }
}

public class SpeechCommand : ICommand
{
    private readonly EntityModel model;
    public SpeechCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        string hash = model.Vectorizer.HashFloatArray(model.LastInput);
        model.Speech.SetOwnSpeech(hash);
        DialogSystem.Instance.OnSpeechSend((IDialogSender)model.Motor, hash);

        var nearest = model.Perception.FindNearestEntity(model.Motor.Position, model.SelfObject, out float dist);
        await model.Speech.PlayAsync(
            hash, model.Stats.Health.Value, model.Stats.Energy.Value,
            0f, 0f, model.LastActionIndex, dist, default);

        model.Stats.Energy.Value -= 5f;
        model.Brain.GiveReward(0.55f);
    }

    public void Cancel() { }
}
public class MimicCommand : ICommand
{
    private readonly EntityModel model;
    public MimicCommand(EntityModel model) => this.model = model;

    public UniTask ExecuteAsync()
    {
        var target = model.Perception.FindNearestEntity(model.Motor.Position, model.SelfObject, out _);
        var otherModel = target?.GetComponent<EntityViewModel>()?.Entity as EntityModel;

        if (otherModel == null) { model.Brain.GiveReward(0.2f); return UniTask.CompletedTask; }

        model.SetLastAction(otherModel.LastActionIndex);
        float reward = 0.3f + otherModel.Brain.Context.CoordMLP.AverageEntropy * 0.2f;
        model.Brain.GiveReward(reward);
        model.Feedback.FlashColor(Color.white, 0).Forget();

        return UniTask.CompletedTask;
    }
    public void Cancel() { }
}

public class ThreatenCommand : ICommand
{
    private readonly EntityModel model;
    public ThreatenCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        var target = model.Perception.FindNearestEntity(model.Motor.Position, model.SelfObject, out float dist);
        if (target == null || dist > model.Tuning.AttackRange * 2f)
        {
            model.Brain.GiveReward(target == null ? 0.2f : 0.15f);
            return;
        }

        model.Feedback.FlashColor(Color.orange, 0).Forget();

        model.Stats.Energy.Value -= 3f;
        model.Brain.GiveReward(dist < model.Tuning.AttackRange ? 0.6f : 0.4f);
        await UniTask.Delay(800);
    }

    public void Cancel() { }
}

public class ShareFoodCommand : ICommand
{
    private readonly EntityModel model;
    public ShareFoodCommand(EntityModel model) => this.model = model;

    public async UniTask ExecuteAsync()
    {
        if (model.Stats.Health.Value < 80f) { model.Brain.GiveReward(0.1f); return; }

        var target = model.Perception.FindNearestEntity(model.Motor.Position, model.SelfObject, out _);
        var victim = target != null ? target.GetComponent<EntityViewModel>()?.Entity as EntityModel : null;

        if (victim == null || victim.Stats.Health.Value > model.Stats.Health.Value * 0.8f)
        {
            model.Brain.GiveReward(0.25f);
            return;
        }

        model.Feedback.FlashColor(Color.yellow, 0).Forget();

        float transfer = Mathf.Min(20f, model.Stats.Health.Value - 60f);
        model.Stats.Health.Value -= transfer;
        victim.Stats.Health.Value = Mathf.Min(victim.Stats.Health.Value + transfer, victim.Stats.MaxHealth);

        victim.Feedback.FlashColor(Color.white, 0).Forget();

        float needFactor = 1f - Mathf.Clamp01(victim.Stats.Health.Value / victim.Stats.MaxHealth);
        model.Brain.GiveReward(0.5f + needFactor * 0.35f);
        await UniTask.Delay(500);
    }

    public void Cancel() { }
}

public class AttackCommand : ICommand
{
    private readonly EntityModel model;
    private CancellationTokenSource cts = new();

    public AttackCommand(EntityModel model) => this.model = model;

    public bool CanExecute() => model.Stats.Energy.Value >= model.Tuning.AttackEnergyCost;

    public async UniTask ExecuteAsync()
    {
        var target = model.Perception.FindNearestEntity(model.Motor.Position, model.SelfObject, out _);
        if (target == null) { model.Brain.GiveReward(0.2f); return; }

        await model.Feedback.FlashColor(Color.red, 0);
        await model.Motor.MoveToAsync(target.transform.position, model.Tuning.MoveSpeed, 2f,
            model.Tuning.EnergyCostMoving, cts.Token);

        if (Vector3.Distance(model.Motor.Position, target.transform.position) <= model.Tuning.AttackRange && model.TryStartAttackCooldown())
        {
            var victim = target.GetComponent<EntityViewModel>()?.Entity as EntityModel;
            if(victim != null) victim.Stats.Health.Value -= model.Tuning.AttackDamage;
            model.Brain.GiveReward(victim != null ? 0.9f : 0.4f);
        }
        else model.Brain.GiveReward(0.3f);
    }

    public void Cancel() => cts.Cancel();
}


public class WanderCommand : ICommand
{
    private readonly EntityModel model;
    private CancellationTokenSource cts = new();

    public WanderCommand(EntityModel _model) => model = _model;

    public async UniTask ExecuteAsync()
    {
        var randomCircle = RavineRandom.GetInsideCircle();
        var dir = new Vector3(randomCircle.x, 0, randomCircle.y).normalized;
        var target = model.Motor.Position + dir * model.Tuning.WanderRadius;

        await model.Motor.MoveToAsync(target, model.Tuning.MoveSpeed,
            RavineRandom.RangeFloat(model.Tuning.MinWanderTime, model.Tuning.MaxWanderTime),
            model.Tuning.EnergyCostMoving, cts.Token);
    }

    public void Cancel() => cts.Cancel();
}

