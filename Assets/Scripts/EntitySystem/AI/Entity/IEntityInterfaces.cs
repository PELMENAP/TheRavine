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

public interface IEntityDialogHost
{
    void RegisterDialog(IDialogListener listener);
    void UnregisterDialog(IDialogListener listener);
    void UpdateDialogPosition(IDialogListener listener);
}

public interface IEntityDeathHandler
{
    void OnDeath();
}

public interface IEntityAudio
{
    UniTask PlaySpeechAsync(string speech, float health, float energy, float danger,
        float timeToBreed, int lastAction, float nearestEnemyDist, CancellationToken ct);
}