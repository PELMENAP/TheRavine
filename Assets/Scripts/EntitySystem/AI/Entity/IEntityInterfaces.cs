using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public interface IEntityMotor
{
    Vector3 Position { get; }
    UniTask MoveToAsync(Vector3 target, float speed, float maxDuration, float energyCostPerSec, CancellationToken ct);
    void Stop();
}

public interface IEntityFeedback
{
    UniTask FlashColor(Color color, int duration);
    void SetLabel(string text);
}