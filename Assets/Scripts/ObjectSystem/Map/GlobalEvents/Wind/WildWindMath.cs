using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static class WildWindMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InBand(float y, float bottom, float top) => y >= bottom && y <= top;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BandFade(float y, float bottom, float top, float fade)
        {
            if (!InBand(y, bottom, top)) return 0f;
            if (fade <= 0f) return 1f;
            return math.saturate(math.min(y - bottom, top - y) / fade);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Falloff(float distance, float radius) =>
            radius > 0f ? math.saturate(1f - distance / radius) : 0f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GustDamage(float distance, float radius, float damagePerSecond, float bandFade) =>
            damagePerSecond * Falloff(distance, radius) * bandFade;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WallZ(in RavineProfile ravine, float height, float side)
        {
            float t = ravine.Height > 0f ? math.saturate(height / ravine.Height) : 1f;
            return side * (ravine.HalfWidth + t * math.rcp(ravine.InvRun));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Reaches(float windowZ, float reach, float halfWidth) => math.abs(windowZ) + reach >= halfWidth;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 StepToward(float3 from, float3 to, float speed, float dt)
        {
            float3 d = to - from;
            float length = math.length(d);
            float move = speed * dt;
            return length <= move || length <= 1e-5f ? to : from + d * (move / length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WrapX(float x, float centerX, float reach)
        {
            float span = 2f * reach;
            if (span <= 0f) return x;
            float local = x - centerX + reach;
            return centerX - reach + (local - math.floor(local / span) * span);
        }
    }
}
