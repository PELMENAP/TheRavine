using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class MoveAlongPathCommand : ICommand
{
    private Transform _transform;
    private List<Vector3> _path;
    private float _speed;
    private System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();

    public MoveAlongPathCommand(Transform transform, List<Vector3> path, float speed)
    {
        _transform = transform;
        _path = path;
        _speed = speed;
    }

    public async UniTask ExecuteAsync()
    {
        foreach (var target in _path)
        {
            if (_cts.IsCancellationRequested) break;
            await MoveToTargetAsync(target);
        }
    }

    private async UniTask MoveToTargetAsync(Vector3 target)
    {
        while (_transform.position != target)
        {
            if (_cts.IsCancellationRequested) break;

            // Перемещение к точке с заданной скоростью
            _transform.position = Vector3.MoveTowards(_transform.position, target, _speed * Time.deltaTime);
            await UniTask.Yield(PlayerLoopTiming.Update); // Ожидание следующего кадра
        }
    }

    public void Cancel()
    {
        _cts.Cancel();
    }
}
