using Unity.Mathematics;
using UnityEngine;

public readonly struct HuntTarget
{
    public readonly EntityModel Entity;
    public readonly Transform   Player;

    public HuntTarget(EntityModel entity, Transform player)
    {
        Entity = entity;
        Player = player;
    }

    public bool IsPlayer => Entity == null && Player != null;

    public bool IsValid => Entity != null
        ? !Entity.IsDisposed && !Entity.IsDeathPending
        : Player != null;

    public Vector3 Position => Entity != null ? Entity.Motor.Position() : Player.position;

    public float2 Position2D
    {
        get
        {
            Vector3 p = Position;
            return new float2(p.x, p.z);
        }
    }
}
