using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static class WeatherMath
    {
        public const float GrowFraction = 0.25f;
        public const float DissipateFraction = 0.4f;
        public const float GustReach = 2.2f;
        public const float SecondsPerHour = 3600f;
        public const float StrokeWidth = 0.035f;
        public const float SecondStroke = 0.22f;
        public const float ThirdStroke = 0.52f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Falloff(float distance, float radius)
        {
            if (!(radius > 0f)) return 0f;
            float t = math.saturate(1f - distance / radius);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Envelope(float age, float life)
        {
            if (!(life > 0f) || age <= 0f || age >= life) return 0f;
            float u = age / life;
            float up = math.smoothstep(0f, GrowFraction, u);
            float down = math.smoothstep(1f, 1f - DissipateFraction, u);
            return up * down;
        }

        public static float Heat(float dayPhase, float season, WeatherRules r)
        {
            float d = math.abs(math.frac(dayPhase - r.heatPeakPhase + 1.5f) - 0.5f);
            float bell = math.exp(-0.5f * (d / r.heatWidth) * (d / r.heatWidth));
            return math.saturate(bell * (1f + r.heatSeasonBias * season));
        }

        public static float ConvectiveRate(WeatherRules r, float dayPhase, float season, bool winter)
            => winter ? 0f : r.convectivePerHour * Heat(dayPhase, season, r);

        public static float StratiformRate(WeatherRules r, float season, bool winter)
            => r.stratiformPerHour * math.max(0f, 1f + r.spawnSeasonBias * season) * (winter ? r.winterPrecipitation : 1f);

        public static float SpawnRate(WeatherRules r, float dayPhase, float season, bool winter)
            => ConvectiveRate(r, dayPhase, season, winter) + StratiformRate(r, season, winter);

        public static float ChargeGrowth(float heat, bool winter, WeatherRules r)
            => winter ? 0f : math.lerp(1f - r.chargeHeatCorrelation, 1f, math.saturate(heat));

        public static float Life(float moistureShare, float jitter, WeatherRules r)
            => math.lerp(r.lifeMin, r.lifeMax,
                math.saturate(r.lifeMoistureWeight * moistureShare + (1f - r.lifeMoistureWeight) * jitter));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Evaporation(float heat, float dryness, float virgaStrength)
            => math.saturate(virgaStrength * heat * dryness);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GroundRain(float moisture, float heat, float dryness, float virgaStrength)
            => moisture * (1f - Evaporation(heat, dryness, virgaStrength));

        public static float GroundRain(float moisture, float heat, float dryness, WeatherRules r)
            => GroundRain(moisture, heat, dryness, r.virgaStrength);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pressure(float distance, float radius, float depth)
        {
            if (!(radius > 0f)) return 0f;
            float x = distance / radius;
            return -depth * math.exp(-2f * x * x);
        }

        public static float2 GustFront(float2 offset, float radius, float strength)
        {
            float d2 = math.lengthsq(offset);
            if (d2 <= 1e-8f || !(radius > 0f)) return float2.zero;

            float d = math.sqrt(d2);
            float x = d / radius;
            float window = math.saturate((GustReach - x) / (GustReach - 1f));
            float magnitude = strength * x * math.exp(0.5f * (1f - x * x)) * window;
            return offset * (magnitude / d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WetnessStep(float wet, float rain, float heat, float wind, float dt,
            float gain, float dryPerSecond, float dryHeat, float dryWind)
        {
            float drying = dryPerSecond * (1f + dryHeat * heat + dryWind * wind);
            return math.saturate(wet + (gain * rain - drying) * dt);
        }

        public static float WetnessStep(float wet, float rain, float heat, float wind, float dt, WeatherRules r)
            => WetnessStep(wet, rain, heat, wind, dt, r.wetnessGain, r.wetnessDryPerSecond, r.wetnessDryHeat, r.wetnessDryWind);

        public static float StrikeRate(float charge, float threshold, float perSecond)
            => perSecond * math.max(0f, charge - threshold);

        public static float StrokeIntensity(float age, float seed, float duration)
        {
            if (!(duration > 0f) || age < 0f || age >= duration) return 0f;

            float u = age / duration;
            float a = u / StrokeWidth;
            float b = (u - (SecondStroke + 0.1f * seed)) / StrokeWidth;
            float c = (u - (ThirdStroke + 0.12f * seed)) / StrokeWidth;
            float strokes = math.exp(-a * a) + 0.7f * math.exp(-b * b) + 0.5f * math.exp(-c * c);
            float glow = 0.35f * math.exp(-u * 5f);
            float fade = 1f - math.smoothstep(0.8f, 1f, u);
            return math.saturate(math.max(strokes, glow)) * fade;
        }

        public static float StrikeDamage(float distance, float radius, float damage, float strength)
            => damage * strength * Falloff(distance, radius);

        public static float StrikeRate(float charge, WeatherRules r)
            => StrikeRate(charge, r.strikeThreshold, r.strikesPerSecond);
    }
}
