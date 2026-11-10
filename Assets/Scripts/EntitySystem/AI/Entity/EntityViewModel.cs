using UnityEngine;

using TheRavine.EntityControl;

public class EntityViewModel : AEntityViewModel, IEntityMotor, IEntityDeathHandler
{
    [SerializeField] private SurfaceMotor motor;
    public void BindMotion(MotionSystem system) => motor.BindMotion(system);

    public void OnDeath()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public Vector3 Position() => motor.Position();
    public void BindSlot(int slot) => motor.BindSlot(slot);

    public void BeginMove(Vector3 target, float speed, float energyCostPerSec, double deadline)
        => motor.BeginMove(target, speed, energyCostPerSec, deadline);

    public void BeginMove(Vector3 target, Vector3 control, float speed, float energyCostPerSec, double deadline)
        => motor.BeginMove(target, control, speed, energyCostPerSec, deadline);

    public bool       IsMoving => motor.IsMoving;
    public MoveResult LastMove => motor.LastMove;
    public float DrainEnergy() => motor.DrainEnergy();
    public void Stop() => motor.Stop();

    protected override void OnViewUpdate() { }
    protected override void OnViewEnable() { }
    protected override void OnViewDisable() { }

    public override void OnNetworkSpawn() { }
}
