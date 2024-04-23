
namespace TheRavine.EntityControl
{
    public class EntityAimBaseStats
    {
        public int crosshairDistanse;
        public int maxCrosshairDistanse;
        public int crosshairOffset;
        public int pickDistance;

        public EntityAimBaseStats(int _crosshairDistanse, int _maxCrosshairDistanse, int _crosshairOffset, int _pickDistance)
        {
            crosshairDistanse = _crosshairDistanse;
            maxCrosshairDistanse = _maxCrosshairDistanse;
            crosshairOffset = _crosshairOffset;
            pickDistance = _pickDistance;

        }

        public EntityAimBaseStats(EntityAimStatsInfo info)
        {
            crosshairDistanse = info.CrosshairDistanse;
            maxCrosshairDistanse = info.MaxCrosshairDistanse;
            crosshairOffset = info.CrosshairDistanse;
            pickDistance = info.PickDistance;
        }
    }
}