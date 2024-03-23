
namespace TheRavine.EntityControl
{
    public class EntityAimBaseStats
    {
        public int crosshairDistanse;
        public int maxCrosshairDistanse;
        public int crosshairOffset;

        public EntityAimBaseStats(int _crosshairDistanse, int _maxCrosshairDistanse, int _crosshairOffset)
        {
            crosshairDistanse = _crosshairDistanse;
            maxCrosshairDistanse = _maxCrosshairDistanse;
            crosshairOffset = _crosshairOffset;
        }

        public EntityAimBaseStats(EntityAimStatsInfo info)
        {
            crosshairDistanse = info.CrosshairDistanse;
            maxCrosshairDistanse = info.MaxCrosshairDistanse;
            crosshairOffset = info.CrosshairDistanse;
        }
    }
}