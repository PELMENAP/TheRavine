using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using TheRavine.Generator;

public class WeatherFieldJobTests
{
    private const int Side = 8;
    private const float Texel = 10f;

    private struct Field
    {
        public NativeArray<float2> Position;
        public NativeArray<float> Radius, Moisture, Charge, Age, Life;
        public NativeArray<float4> MapA, MapB;
        public NativeArray<int2> Keys;

        public static Field Create(int cells)
        {
            var f = new Field
            {
                Position = new NativeArray<float2>(cells, Allocator.Persistent),
                Radius = new NativeArray<float>(cells, Allocator.Persistent),
                Moisture = new NativeArray<float>(cells, Allocator.Persistent),
                Charge = new NativeArray<float>(cells, Allocator.Persistent),
                Age = new NativeArray<float>(cells, Allocator.Persistent),
                Life = new NativeArray<float>(cells, Allocator.Persistent),
                MapA = new NativeArray<float4>(Side * Side, Allocator.Persistent),
                MapB = new NativeArray<float4>(Side * Side, Allocator.Persistent),
                Keys = new NativeArray<int2>(Side * Side, Allocator.Persistent)
            };
            for (int i = 0; i < Side * Side; i++) f.Keys[i] = new int2(int.MinValue, int.MinValue);
            return f;
        }

        public void Dispose()
        {
            Position.Dispose();
            Radius.Dispose();
            Moisture.Dispose();
            Charge.Dispose();
            Age.Dispose();
            Life.Dispose();
            MapA.Dispose();
            MapB.Dispose();
            Keys.Dispose();
        }

        public void SetCell(float2 position, float radius, float moisture, float charge)
        {
            Position[0] = position;
            Radius[0] = radius;
            Moisture[0] = moisture;
            Charge[0] = charge;
            Age[0] = 50f;
            Life[0] = 100f;
        }

        public void Run(int count, int2 origin, float heat, float dt)
        {
            new WeatherFieldJob
            {
                Position = Position,
                Radius = Radius,
                Moisture = Moisture,
                Charge = Charge,
                Age = Age,
                Life = Life,
                Count = count,
                MapA = MapA,
                MapB = MapB,
                Keys = Keys,
                Side = Side,
                Origin = origin,
                Texel = Texel,
                Wind = float2.zero,
                Heat = heat,
                Dt = dt,
                Rules = Params()
            }.Run(Side * Side);
        }

        public static WeatherFieldParams Params()
        {
            var r = new WeatherRules();
            return new WeatherFieldParams
            {
                Virga = r.virgaStrength,
                WetnessGain = r.wetnessGain,
                WetnessDry = r.wetnessDryPerSecond,
                WetnessDryHeat = r.wetnessDryHeat,
                WetnessDryWind = r.wetnessDryWind,
                PressurePerMoisture = r.pressurePerMoisture,
                PressurePerCharge = r.pressurePerCharge,
                GustStrength = r.gustStrength,
                CloudSpread = r.cloudSpread,
                CloudBase = r.cloudBase
            };
        }
    }

    [Test]
    public void CentreTexelMatchesTheManualFormulas()
    {
        var f = Field.Create(1);
        try
        {
            f.SetCell(new float2(35f, 35f), 30f, 0.8f, 0.5f);
            f.Run(1, int2.zero, 0f, 0.5f);

            float4 a = f.MapA[3 + 3 * Side];
            Assert.AreEqual(0.8f, a.x, 1e-4f);
            Assert.AreEqual(0.5f, a.z, 1e-4f);
            Assert.Greater(a.y, 0.4f);
            Assert.Greater(a.w, 0f);

            float4 far = f.MapA[7 + 7 * Side];
            Assert.AreEqual(0f, far.x, 1e-6f);
            Assert.AreEqual(0f, f.MapB[3 + 3 * Side].x, 1e-5f);
            Assert.Less(f.MapB[3 + 3 * Side].z, 0f);
        }
        finally { f.Dispose(); }
    }

    [Test]
    public void HotDryAirTurnsRainIntoVirga()
    {
        var f = Field.Create(1);
        try
        {
            f.SetCell(new float2(35f, 35f), 30f, 0.8f, 0.9f);
            f.Run(1, int2.zero, 1f, 0.5f);

            float4 a = f.MapA[3 + 3 * Side];
            Assert.Less(a.x, 0.8f * 0.25f);
            Assert.AreEqual(0.9f, a.z, 1e-4f);
        }
        finally { f.Dispose(); }
    }

    [Test]
    public void GustFrontPointsAwayFromTheCellCentre()
    {
        var f = Field.Create(1);
        try
        {
            f.SetCell(new float2(35f, 35f), 20f, 0.5f, 1f);
            f.Run(1, int2.zero, 0f, 0.5f);

            Assert.Greater(f.MapB[5 + 3 * Side].x, 0f);
            Assert.Less(f.MapB[1 + 3 * Side].x, 0f);
            Assert.Greater(f.MapB[3 + 5 * Side].y, 0f);
            Assert.Less(f.MapB[3 + 1 * Side].y, 0f);
        }
        finally { f.Dispose(); }
    }

    [Test]
    public void WetnessSurvivesWhileTheKeyHoldsAndResetsWhenTheWindowMoves()
    {
        var f = Field.Create(1);
        try
        {
            f.SetCell(new float2(35f, 35f), 30f, 0.8f, 0f);
            for (int i = 0; i < 20; i++) f.Run(1, int2.zero, 0f, 0.5f);
            float wet = f.MapA[3 + 3 * Side].w;
            Assert.Greater(wet, 0.1f);

            f.Run(0, int2.zero, 0f, 0.5f);
            Assert.Greater(f.MapA[3 + 3 * Side].w, 0.1f);
            Assert.LessOrEqual(f.MapA[3 + 3 * Side].w, wet);

            f.Run(0, new int2(1, 0), 0f, 0.5f);
            Assert.AreEqual(new int2(8, 0), f.Keys[0]);
            Assert.AreEqual(0f, f.MapA[0].w, 1e-6f);
            Assert.AreEqual(new int2(3, 3), f.Keys[3 + 3 * Side]);
            Assert.Greater(f.MapA[3 + 3 * Side].w, 0.05f);
        }
        finally { f.Dispose(); }
    }

    [Test]
    public void WeatherMathDoesNotAllocate()
    {
        var r = new WeatherRules();
        float sink = 0f;
        AllocationAssert.Zero(() =>
        {
            sink += WeatherMath.Falloff(10f, 100f) + WeatherMath.Envelope(30f, 100f)
                  + WeatherMath.GroundRain(0.5f, 0.5f, 0.5f, r.virgaStrength)
                  + WeatherMath.Pressure(10f, 100f, 5f) + math.length(WeatherMath.GustFront(new float2(50f, 20f), 100f, 1f))
                  + WeatherMath.WetnessStep(0.2f, 0.5f, 0.5f, 0.1f, 0.5f, r.wetnessGain, r.wetnessDryPerSecond, r.wetnessDryHeat, r.wetnessDryWind)
                  + WeatherMath.StrikeRate(0.8f, r.strikeThreshold, r.strikesPerSecond)
                  + WeatherMath.StrokeIntensity(0.1f, 0.3f, 0.3f);
        }, 1000, "WeatherMath");
        Assert.Greater(sink, 0f);
    }
}
