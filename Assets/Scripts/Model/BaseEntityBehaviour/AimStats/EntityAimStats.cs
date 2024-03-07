
namespace TheRavine.EntityControl
{
    public class EntityAimBaseStats
    {
        public int crosshairDistanse;
        public int maxCrosshairDistanse;

        public EntityAimBaseStats(int _crosshairDistanse, int _maxCrosshairDistanse)
        {
            crosshairDistanse = _crosshairDistanse;
            maxCrosshairDistanse = _maxCrosshairDistanse;
        }

        public EntityAimBaseStats(EntityAimStatsInfo info)
        {
            crosshairDistanse = info.CrosshairDistanse;
            maxCrosshairDistanse = info.MaxCrosshairDistanse;
        }
    }
}