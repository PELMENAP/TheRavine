using Unity.Mathematics;

using TheRavine.ViewControl;

namespace TheRavine.EntityControl
{
    public struct ViewDragGesture
    {
        public const float SquareFraction = 0.9f;

        private float2 start;
        private float2 last;
        private float side;
        private bool tracking;
        private bool armed;
        private bool moved;

        public bool IsTracking => tracking;
        public bool IsDragging => tracking && armed && moved;
        public float Side => side;

        public static float SquareSide(float2 screen) => SquareFraction * math.min(screen.x, screen.y);

        public static bool InsideSquare(float2 position, float2 screen)
        {
            float half = SquareSide(screen) * 0.5f;
            return math.abs(position.x - screen.x * 0.5f) <= half
                && math.abs(position.y - screen.y * 0.5f) <= half;
        }

        public void Begin(float2 position, float2 screen, bool overUi)
        {
            tracking = true;
            moved = false;
            start = position;
            last = position;
            side = SquareSide(screen);
            armed = side > 0f && !overUi && InsideSquare(position, screen);
        }

        public bool Move(float2 position, float thresholdPixels, out float2 delta)
        {
            delta = float2.zero;
            if (!tracking) return false;

            if (!moved)
            {
                if (math.lengthsq(position - start) <= thresholdPixels * thresholdPixels) return false;
                moved = true;
                last = start;
            }

            delta = position - last;
            last = position;
            return armed && math.any(delta != 0f);
        }

        public bool End(bool canceled)
        {
            bool click = tracking && !moved && !canceled;
            tracking = false;
            armed = false;
            moved = false;
            return click;
        }

        public static void Rotate(ref float yaw, ref float pitch, float2 deltaPixels, float side,
            float yawDegreesPerSide, float pitchDegreesPerSide, float pitchMin, float pitchMax)
        {
            if (side <= 0f) return;

            yaw = ViewSector.Wrap(yaw - deltaPixels.x / side * yawDegreesPerSide);
            pitch = math.clamp(pitch + deltaPixels.y / side * pitchDegreesPerSide, pitchMin, pitchMax);
        }
    }
}
