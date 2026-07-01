using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class AttackCommand : ICommand
{
    private readonly EntityModel model;
    private CancellationTokenSource cts = new();

    public AttackCommand(EntityModel model) => model = model;

    public async UniTask ExecuteAsync()
    {
        var target = model.Perception.FindNearestEntity(model.Motor.Position, model.SelfObject, out _);
        if (target == null) { model.Brain.GiveReward(0.2f); return; }

        model.Feedback.FlashColor(Color.red, 0f);
        await model.Motor.MoveToAsync(target.transform.position, model.Tuning.MoveSpeed, 2f,
            model.Tuning.EnergyCostMoving, cts.Token);

        if (Vector3.Distance(model.Motor.Position, target.transform.position) <= model.Tuning.AttackRange)
        {
            var victim = target.GetComponent<EntityViewModel>()?.Entity as EntityModel;
            victim?.Stats.Health.Value -= model.Tuning.AttackDamage;
            model.Brain.GiveReward(victim != null ? 0.9f : 0.4f);
        }
        else model.Brain.GiveReward(0.3f);
    }

    public void Cancel() => cts.Cancel();
}