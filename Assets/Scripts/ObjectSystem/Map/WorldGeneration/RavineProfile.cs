using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public readonly struct RavineProfile
    {
        public readonly float HalfWidth;
        public readonly float InvRun;
        public readonly float Height;

        public RavineProfile(float halfWidth, float wallRun, float wallHeight)
        {
            HalfWidth = math.max(halfWidth, 0f);
            InvRun = math.rcp(math.max(wallRun, 1e-3f));
            Height = math.max(wallHeight, 0f);
        }

        public static RavineProfile From(ChunkGenerationSettings settings) =>
            new(settings.ravineHalfWidth, settings.ravineWallRun, settings.ravineWallHeight);

        public bool IsFlat => Height <= 0f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Blend(float z) =>
            math.select(math.saturate((math.abs(z) - HalfWidth) * InvRun), 0f, Height <= 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Elevation(float z) => Height * Blend(z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 ElevationRange(float z0, float z1)
        {
            float a = Elevation(z0);
            float b = Elevation(z1);
            float low = math.select(math.min(a, b), 0f, z0 <= 0f && z1 >= 0f);
            return new float2(low, math.max(a, b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValley(float t) => t <= 0f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWall(float t) => t > 0f && t < 1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPlateau(float t) => t >= 1f;
    }
}
