using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class SpawnObjectsCommand : ICommand
{
    private GameObject _prefab;
    private List<Vector3> _positions;
    private System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();

    public SpawnObjectsCommand(GameObject prefab, List<Vector3> positions)
    {
        _prefab = prefab;
        _positions = positions;
    }

    public async UniTask ExecuteAsync()
    {
        foreach (var position in _positions)
        {
            if (_cts.IsCancellationRequested) break;

            Object.Instantiate(_prefab, position, Quaternion.identity);
            await UniTask.Delay(500, cancellationToken: _cts.Token);
        }
    }

    public void Cancel()
    {
        _cts.Cancel();
    }
}
