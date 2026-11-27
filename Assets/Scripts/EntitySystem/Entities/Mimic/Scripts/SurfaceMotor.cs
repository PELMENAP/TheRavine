using UnityEngine;
using Unity.Mathematics;

public class SurfaceMotor : MonoBehaviour, IEntityMotor, IVelocitySource
{
    [SerializeField] private float heightOffset = 8.8f;
    [SerializeField] private float velocityLerpCoef = 4f;
    [SerializeField] private float arriveThreshold = 0.1f;

    private MotionSystem motion;
    private Transform    _tr;

    private float _distance;
    private float _pathCost;
    private float _energy;
    private float _pendingEnergy;
    private bool  _arrived;
    private bool  _blocked;

    private float3 _detached;

    internal int MotionIndex = -1;
    internal int Slot        = -1;

    public bool       IsMoving => MotionIndex >= 0;
    public MoveResult LastMove => new(_distance, _pathCost, _energy, _arrived, _blocked);

    public Vector3 Velocity
    {
        get
        {
            float2 v = IsMoving ? motion.VelocityOf(MotionIndex) : float2.zero;
            return new Vector3(v.x, 0f, v.y);
        }
    }

    private void Awake()
    {
        _tr       = transform;
        _detached = _tr.position;
    }

    private void OnDisable() => Stop();

    public void BindMotion(MotionSystem system) => motion = system;

    public void BindSlot(int slot)
    {
        if (slot < 0 && Slot >= 0 && motion != null) _detached = motion.PositionOf(Slot);
        Slot = slot;
    }

    public Vector3 Position() => Slot >= 0 && motion != null ? motion.PositionOf(Slot) : _detached;

    public void BeginMove(Vector3 target, float speed, float energyCostPerSec, double deadline)
    {
        Vector3 from = Position();
        BeginMove(target, (from + target) * 0.5f, speed, energyCostPerSec, deadline);
    }

    public void BeginMove(Vector3 target, Vector3 control, float speed, float energyCostPerSec, double deadline)
    {
        _distance = 0f;
        _pathCost = 0f;
        _energy   = 0f;
        _arrived  = false;
        _blocked  = false;

        if (motion == null || _tr == null || Slot < 0)
        {
            Stop();
            return;
        }

        float3 pos   = Position();
        float2 start = pos.xz;
        float2 ctrl  = new float2(control.x, control.z);
        float2 goal  = new float2(target.x, target.z);
        float  len   = 0.5f * (math.distance(start, goal) + math.distance(start, ctrl) + math.distance(ctrl, goal));

        var state = new MotionState
        {
            Start         = start,
            Control       = ctrl,
            Goal          = goal,
            InvLength     = len > 1e-4f ? 1f / len : 0f,
            T             = 0f,
            Velocity      = float2.zero,
            Deadline      = deadline,
            Speed         = speed,
            EnergyCost    = energyCostPerSec,
            SpeedModifier = 1f,
            WaterSpeed    = 1f,
            WaterDrain    = 1f,
            Arrive2       = arriveThreshold * arriveThreshold,
            HeightOffset  = heightOffset,
            VelocityLerp  = velocityLerpCoef,
        };

        if (!motion.Start(this, _tr, in state)) Stop();
    }

    public float DrainEnergy()
    {
        float e = _pendingEnergy;
        _pendingEnergy = 0f;
        if (IsMoving) e += motion.TakePending(MotionIndex);
        return e;
    }

    public void Stop()
    {
        if (IsMoving) motion.Stop(this);
    }

    internal void OnMotionFinished(in MotionState s)
    {
        _distance       = s.Distance;
        _pathCost       = s.PathCost;
        _energy         = s.Energy;
        _arrived        = s.Arrived != 0;
        _blocked        = s.Blocked != 0;
        _pendingEnergy += s.Pending;
    }
}