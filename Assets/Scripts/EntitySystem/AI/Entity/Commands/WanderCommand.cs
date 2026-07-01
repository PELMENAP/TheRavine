using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

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