namespace TheRavine.EntityControl
{
    public interface IAimComponent : IComponent 
    {
        EntityAimBaseStats BaseStats { get; set; }
    }

    public class AimComponent : IAimComponent
    {
        public EntityAimBaseStats BaseStats { get; set; }
        public AimComponent(EntityAimBaseStats _baseStats)
        {
            BaseStats = _baseStats;
        }

        public void Dispose()
        {

        }
    }
}