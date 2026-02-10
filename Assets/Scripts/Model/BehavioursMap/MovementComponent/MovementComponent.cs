using UnityEngine;

public interface IMovementComponent : IComponent
{
    void SetBaseSpeed(float baseSpeed);
}

public class MovementComponent : IMovementComponent
{
    public float BaseSpeed { get; private set; } 
    public float Acceleration { get; private set; }
    public float Deceleration { get; private set; }
    private Vector2 velocity;
    public MovementComponent(EntityMovementInfo info)
    {
        BaseSpeed = info.BaseSpeed;
        Acceleration = info.Acceleration;
        Deceleration = info.Deceleration;
    }
    public void SetBaseSpeed(float baseSpeed) => BaseSpeed = Mathf.Max(0, baseSpeed);

    public void SetVelocity(Vector2 v) => velocity = v;

    public Vector2 GetEntityVelocity() => velocity;
    public void Dispose()
    {
    }
}