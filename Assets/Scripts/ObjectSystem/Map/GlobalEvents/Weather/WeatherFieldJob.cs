using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public struct WeatherFieldParams
    {
        public float Virga;
        public float WetnessGain;
        public float WetnessDry;
        public float WetnessDryHeat;
        public float WetnessDryWind;
        public float PressurePerMoisture;
        public float PressurePerCharge;
        public float GustStrength;
        public float CloudSpread;
        public float CloudBase;
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct WeatherFieldJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Position;
        [ReadOnly] public NativeArray<float> Radius;
        [ReadOnly] public NativeArray<float> Moisture;
        [ReadOnly] public NativeArray<float> Charge;
        [ReadOnly] public NativeArray<float> Age;
        [ReadOnly] public NativeArray<float> Life;
        public int Count;

        public NativeArray<float4> MapA;
        public NativeArray<float4> MapB;
        public NativeArray<int2> Keys;
        public int Side;
        public int2 Origin;
        public float Texel;
        public float2 Wind;
        public float Heat;
        public float Dt;
        public WeatherFieldParams Rules;

        public void Execute(int slot)
        {
            int sx = slot % Side;
            int sz = slot / Side;
            int kx = Origin.x + Mod(sx - Origin.x, Side);
            int kz = Origin.y + Mod(sz - Origin.y, Side);

            float4 previous = MapA[slot];
            int2 key = new int2(kx, kz);
            if (Keys[slot].x != kx || Keys[slot].y != kz)
            {
                previous = float4.zero;
                Keys[slot] = key;
            }

            float2 p = (new float2(kx, kz) + 0.5f) * Texel;
            float dryness = 1f - previous.w;
            float rain = 0f, cloud = 0f, charge = 0f, pressure = 0f;
            float2 gust = float2.zero;

            for (int i = 0; i < Count; i++)
            {
                float envelope = WeatherMath.Envelope(Age[i], Life[i]);
                if (envelope <= 0f) continue;

                float radius = Radius[i];
                float2 offset = p - Position[i];
                float distance = math.length(offset);
                float reach = radius * math.max(WeatherMath.GustReach, Rules.CloudSpread);
                if (distance >= reach) continue;

                float moisture = Moisture[i];
                float cellCharge = Charge[i];
                float core = WeatherMath.Falloff(distance, radius) * envelope;
                rain += WeatherMath.GroundRain(moisture, Heat, dryness, Rules.Virga) * core;
                charge += cellCharge * core;
                cloud += WeatherMath.Falloff(distance, radius * Rules.CloudSpread) * envelope
                       * (Rules.CloudBase + (1f - Rules.CloudBase) * math.saturate(math.max(moisture, cellCharge)));
                pressure += WeatherMath.Pressure(distance, radius,
                    envelope * (Rules.PressurePerMoisture * moisture + Rules.PressurePerCharge * cellCharge));
                gust += WeatherMath.GustFront(offset, radius, Rules.GustStrength * cellCharge * envelope);
            }

            rain = math.saturate(rain);
            cloud = math.saturate(cloud);
            charge = math.saturate(charge);
            float wet = WeatherMath.WetnessStep(previous.w, rain, Heat, math.length(Wind + gust), Dt,
                Rules.WetnessGain, Rules.WetnessDry, Rules.WetnessDryHeat, Rules.WetnessDryWind);

            MapA[slot] = new float4(rain, cloud, charge, wet);
            MapB[slot] = new float4(gust, pressure, 0f);
        }

        private static int Mod(int a, int n)
        {
            int m = a % n;
            return m < 0 ? m + n : m;
        }
    }
}
