using UnityEngine;

namespace TheRavine.EntityControl
{
    public interface IReadOnlyAimComponent : IComponent
    {
        int CrosshairDistance { get; }
        int MaxCrosshairDistance { get; }
        int CrosshairOffset { get; }
        int PickDistance { get; }
    }

    public interface IAimComponent : IReadOnlyAimComponent
    {
        void SetCrosshairDistance(int distance);
        void SetMaxCrosshairDistance(int maxDistance);
        void SetCrosshairOffset(int offset);
        void SetPickDistance(int distance);
    }

    public sealed class AimComponent : IAimComponent
    {
        public int CrosshairDistance { get; private set; }
        public int MaxCrosshairDistance { get; private set; }
        public int CrosshairOffset { get; private set; }
        public int PickDistance { get; private set; }

        public AimComponent(EntityAimInfo info)
        {
            CrosshairDistance = info.CrosshairDistance;
            MaxCrosshairDistance = info.MaxCrosshairDistance;
            CrosshairOffset = info.CrosshairOffset;
            PickDistance = info.PickDistance;
        }

        public void SetCrosshairDistance(int distance) => CrosshairDistance = Mathf.Max(0, distance);

        public void SetMaxCrosshairDistance(int maxDistance) => MaxCrosshairDistance = Mathf.Max(0, maxDistance);

        public void SetCrosshairOffset(int offset) => CrosshairOffset = offset;

        public void SetPickDistance(int distance) => PickDistance = Mathf.Max(0, distance);

        public void Dispose()
        {

        }
    }
}