using R3;

public interface IMovementComponent : IComponent
{
    ReactiveProperty<float> BaseSpeed { get; }
    IEntityController EntityController { get; set; }
}

public class MovementComponent : IMovementComponent
{
    public ReactiveProperty<float> BaseSpeed { get; }
    public IEntityController EntityController { get; set; }

    public MovementComponent(int _baseSpeed, IEntityController _entityController)
    {
        BaseSpeed.Value = _baseSpeed;
        EntityController = _entityController;
    }

    public MovementComponent(EntityMovementInfo info, IEntityController _entityController)
    {
        BaseSpeed.Value = info.BaseSpeed;
        EntityController = _entityController;
    }

    public void Dispose()
    {

    }
}