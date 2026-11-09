using UnityEngine;

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

public interface IEntityDeathHandler
{
    void OnDeath();
}
