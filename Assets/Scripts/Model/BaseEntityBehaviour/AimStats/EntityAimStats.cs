
namespace TheRavine.EntityControl
{
    public class EntityAimBaseStats
    {
        public int crosshairDistance;
        public int maxCrosshairDistance;
        public int crosshairOffset;
        public int pickDistance;

        public EntityAimBaseStats(int _crosshairDistance, int _maxCrosshairDistance, int _crosshairOffset, int _pickDistance)
        {
            crosshairDistance = _crosshairDistance;
            maxCrosshairDistance = _maxCrosshairDistance;
            crosshairOffset = _crosshairOffset;
            pickDistance = _pickDistance;

        }

        public EntityAimBaseStats(EntityAimStatsInfo info)
        {
            crosshairDistance = info.CrosshairDistance;
            maxCrosshairDistance = info.MaxCrosshairDistance;
            crosshairOffset = info.CrosshairDistance;
            pickDistance = info.PickDistance;
        }
    }
}