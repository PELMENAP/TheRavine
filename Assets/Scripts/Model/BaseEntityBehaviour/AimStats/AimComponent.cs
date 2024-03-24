using TheRavine.EntityControl;
public interface IAimComponent : IComponent
{
    EntityAimBaseStats baseStats { get; set; }
}

public class AimComponent : IAimComponent
{
    public EntityAimBaseStats baseStats { get; set; }
    public AimComponent(EntityAimBaseStats _baseStats)
    {
        baseStats = _baseStats;
    }

    public void Dispose()
    {

    }
}