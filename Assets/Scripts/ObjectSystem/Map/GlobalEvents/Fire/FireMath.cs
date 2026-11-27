using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static class FireMath
    {
        public static float Size(in CollisionShape shape, FireRules r)
            => math.clamp(shape.Kind == CollisionKind.None || !(shape.Height > 0f) ? r.defaultSize : shape.Height, r.minSize, math.max(r.minSize, r.maxSize));

        public static float BurnSeconds(float size, FireRules r)
            => r.burnSeconds * math.pow(math.max(size, 1e-3f), r.burnSizeExponent);

        public static float SpreadReach(float size, FireRules r)
            => math.clamp(r.spreadReachPerSize * size, 1f, math.max(1f, r.spreadMaxReach));

        public static int SpreadAttempts(float size, FireRules r)
            => math.clamp((int)math.round(r.spreadAttemptsPerSize * size), 1, math.max(1, r.spreadMaxAttempts));

        public static float SpreadFalloff(float distance, float reach)
            => math.saturate(1f - distance / (reach + 1f));

        public static float HeatRadius(float size, FireRules r)
            => MapGenerator.scale * 0.5f + r.heatRadiusPerSize * size;

        public static float HeatCover(float distance, float radius)
            => radius > 0f ? math.saturate((radius - distance) / (radius * 0.5f)) : 0f;

        public static float QuenchNeed(float size, FireRules r)
            => r.quenchPerSize * math.pow(math.max(size, 1e-3f), r.quenchSizeExponent);

        public static float RainDamp(float rain, float wetnessTarget, FireRules r)
        {
            float limit = math.max(r.wetIgnitionLimit, 1e-3f);
            if (wetnessTarget >= limit) return 0f;
            return math.saturate(1f - r.rainSpreadDamp * rain) * (1f - math.max(wetnessTarget, 0f) / limit);
        }

        public static float BurnRate(float rain, FireRules r)
            => math.saturate(1f - r.rainSlow * rain);

        public static float SeasonFactor(float season, bool winter, float bias)
            => winter ? 0f : math.max(0f, 1f + bias * season);
    }
}
