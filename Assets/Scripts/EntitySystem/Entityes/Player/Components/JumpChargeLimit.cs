using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    public static class JumpChargeLimit
    {
        public static float Clamp(float charge, double elapsedSinceLastJump, float maxChargeTime, float slack)
        {
            if (!math.isfinite(charge) || !(maxChargeTime > 0f)) return 0f;

            float held = (float)math.min(math.max(elapsedSinceLastJump, 0.0), maxChargeTime);
            float allowed = math.saturate((held + math.max(slack, 0f)) / maxChargeTime);
            return math.min(math.saturate(charge), allowed);
        }
    }
}
