using UnityEngine;

public interface IReadOnlyMovementComponent : IComponent
{
    float BaseSpeed { get; }
    float Acceleration { get; }
    float Deceleration { get; }
    IEntityController EntityController { get; }
}
public interface IMovementComponent : IComponent
{
    void SetBaseSpeed(float baseSpeed);
}

public class MovementComponent : IMovementComponent
{
    public float BaseSpeed { get; private set; } 
    public float Acceleration { get; private set; }
    public float Deceleration { get; private set; }
    public IEntityController EntityController { get; private set; }

    public MovementComponent(EntityMovementInfo info, IEntityController _entityController)
    {
        BaseSpeed = info.BaseSpeed;
        Acceleration = info.Acceleration;
        Deceleration = info.Deceleration;
        EntityController = _entityController;
    }
    public void SetBaseSpeed(float baseSpeed) => BaseSpeed = Mathf.Max(0, baseSpeed);
    public void Dispose()
    {
    }
}