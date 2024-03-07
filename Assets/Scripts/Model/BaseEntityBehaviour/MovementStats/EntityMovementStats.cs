
namespace TheRavine.EntityControl
{
    public class EntityMovementBaseStats
    {
        public int baseSpeed;

        public EntityMovementBaseStats(int _baseSpeed)
        {
            baseSpeed = _baseSpeed;
        }

        public EntityMovementBaseStats(EntityMovementStatsInfo info)
        {
            baseSpeed = info.BaseSpeed;
        }
    }
}