using UnityEngine;

using TheRavine.EntityControl;

public class EntityViewModel : AEntityViewModel, IEntityMotor, IEntityDeathHandler
{
    [SerializeField] private SurfaceMotor motor;
    public void BindMotion(MotionSystem system) => motor.BindMotion(system);

    private Transform _tr;
    private Vector3   _lastPosition;
    private bool      _destroyed;

    private void Awake() => _tr = transform;

    public override void OnDestroy()
    {
        _destroyed = true;
        base.OnDestroy();
    }

    public void OnDeath()
    {
        if (!_destroyed) _lastPosition = _tr.position;
        _destroyed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public Vector3 Position()
    {
        if (_destroyed || _tr == null) return _lastPosition;
        return _lastPosition = _tr.position;
    }

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
