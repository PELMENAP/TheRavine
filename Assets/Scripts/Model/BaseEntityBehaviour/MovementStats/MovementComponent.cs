using TheRavine.EntityControl;
public interface IMovementComponent : IComponent
{
    EntityMovementBaseStats baseStats { get; set; }
}

public class MovementComponent : IMovementComponent
{
    public EntityMovementBaseStats baseStats { get; set; }
    public MovementComponent(EntityMovementBaseStats _baseStats)
    {
        baseStats = _baseStats;
    }

    public void Dispose()
    {

    }
}