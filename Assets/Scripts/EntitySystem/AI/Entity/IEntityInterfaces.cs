using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public interface IEnergySink
{
    bool TryConsume(float amount);
}
public interface IEntityMotor
{
    Vector3 Position();
    void BeginMove(Vector3 target, float speed, float energyCostPerSec, double deadline);
    void BeginMove(Vector3 target, Vector3 control, float speed, float energyCostPerSec, double deadline);
    bool IsMoving { get; }
    MoveResult LastMove { get; }
    float DrainEnergy();
    void Stop();
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