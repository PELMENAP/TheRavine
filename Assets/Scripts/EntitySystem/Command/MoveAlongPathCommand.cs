using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;

public class MoveAlongPathCommand : ICommand
{
    private Transform transform;
    private List<Vector3> path;
    private readonly float speed;
    private ILogger logger;
    private CancellationTokenSource cts = new CancellationTokenSource();

    public MoveAlongPathCommand(Transform transform, List<Vector3> path, float speed, ILogger logger)
    {
        this.transform = transform;
        this.path = path;
        this.speed = speed;
    }

    public async UniTask ExecuteAsync()
    {
        foreach (var target in path)
        {
            if (cts.IsCancellationRequested) break;
            await MoveToTargetAsync(target);
        }
    }

    private async UniTask MoveToTargetAsync(Vector3 target)
    {
        try
        {
            while (transform.position != target)
            {
                if (cts.IsCancellationRequested) break;

                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to process command MoveToTargetAsync: {ex.Message}");
        }
    }

    public void Cancel()
    {
        cts.Cancel();
    }
}
