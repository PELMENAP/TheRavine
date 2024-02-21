
namespace TheRavine.EntityControl
{
    public struct EntityMovementStats
    {
        public int baseSpeed;

        public EntityMovementStats(int _baseSpeed)
        {
            baseSpeed = _baseSpeed;
        }

        public EntityMovementStats(EntityMovementStatsInfo info)
        {
            baseSpeed = info.BaseSpeed;
        }
    }
}