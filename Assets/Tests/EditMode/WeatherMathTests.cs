using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.Generator;

public class WeatherMathTests
{
    private static WeatherRules Rules() => new();

    [Test]
    public void FalloffIsOneAtCentreAndZeroFromTheRadius()
    {
        Assert.AreEqual(1f, WeatherMath.Falloff(0f, 100f), 1e-6f);
        Assert.AreEqual(0.5f, WeatherMath.Falloff(50f, 100f), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.Falloff(100f, 100f), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.Falloff(250f, 100f), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.Falloff(0f, 0f), 1e-6f);
        Assert.Greater(WeatherMath.Falloff(20f, 100f), WeatherMath.Falloff(60f, 100f));
    }

    [Test]
    public void EnvelopeGrowsMaturesAndDissipates()
    {
        Assert.AreEqual(0f, WeatherMath.Envelope(0f, 100f), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.Envelope(100f, 100f), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.Envelope(150f, 100f), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.Envelope(10f, 0f), 1e-6f);
        Assert.AreEqual(1f, WeatherMath.Envelope(50f, 100f), 1e-6f);
        Assert.Greater(WeatherMath.Envelope(15f, 100f), WeatherMath.Envelope(5f, 100f));
        Assert.Greater(WeatherMath.Envelope(70f, 100f), WeatherMath.Envelope(95f, 100f));
    }

    [Test]
    public void HeatPeaksAtTheConfiguredPhaseAndWrapsAroundMidnight()
    {
        WeatherRules r = Rules();
        float peak = WeatherMath.Heat(r.heatPeakPhase, 0f, r);
        Assert.AreEqual(1f, peak, 1e-5f);
        Assert.Less(WeatherMath.Heat(r.heatPeakPhase + 0.5f, 0f, r), 0.01f);
        Assert.AreEqual(WeatherMath.Heat(0.99f, 0f, r), WeatherMath.Heat(-0.01f, 0f, r), 1e-5f);
        Assert.Greater(WeatherMath.Heat(r.heatPeakPhase, 1f, r), WeatherMath.Heat(r.heatPeakPhase + 0.1f, -1f, r));
        Assert.LessOrEqual(WeatherMath.Heat(r.heatPeakPhase, 1f, r), 1f);
    }

    [Test]
    public void StratiformRateDoesNotDependOnTheDayPhase()
    {
        WeatherRules r = Rules();
        r.convectivePerHour = 0f;
        float noon = WeatherMath.SpawnRate(r, r.heatPeakPhase, 0f, false);
        float night = WeatherMath.SpawnRate(r, r.heatPeakPhase + 0.5f, 0f, false);
        Assert.AreEqual(r.stratiformPerHour, noon, 1e-4f);
        Assert.AreEqual(noon, night, 1e-6f);
    }

    [Test]
    public void ConvectiveRateFollowsHeatAndStopsInWinter()
    {
        WeatherRules r = Rules();
        float hot = WeatherMath.ConvectiveRate(r, r.heatPeakPhase, 0f, false);
        float cool = WeatherMath.ConvectiveRate(r, r.heatPeakPhase + 0.5f, 0f, false);
        Assert.Greater(hot, cool);
        Assert.AreEqual(0f, WeatherMath.ConvectiveRate(r, r.heatPeakPhase, 0f, true), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.ChargeGrowth(1f, true, r), 1e-6f);
        Assert.AreEqual(r.stratiformPerHour * r.winterPrecipitation,
            WeatherMath.SpawnRate(r, r.heatPeakPhase, 0f, true), 1e-4f);
    }

    [Test]
    public void ChargeIsScaledByHeatAndMoistureIsNot()
    {
        WeatherRules r = Rules();
        Assert.AreEqual(1f, WeatherMath.ChargeGrowth(1f, false, r), 1e-6f);
        Assert.AreEqual(1f - r.chargeHeatCorrelation, WeatherMath.ChargeGrowth(0f, false, r), 1e-6f);
        Assert.Greater(WeatherMath.ChargeGrowth(0.8f, false, r), WeatherMath.ChargeGrowth(0.2f, false, r));
    }

    [Test]
    public void LightRainLivesShorterThanHeavyRain()
    {
        WeatherRules r = Rules();
        Assert.Less(WeatherMath.Life(0f, 0.5f, r), WeatherMath.Life(1f, 0.5f, r));
        Assert.GreaterOrEqual(WeatherMath.Life(0f, 0f, r), r.lifeMin - 1e-4f);
        Assert.LessOrEqual(WeatherMath.Life(1f, 1f, r), r.lifeMax + 1e-4f);
    }

    [Test]
    public void GroundRainIsMoistureWithoutEvaporationAndZeroUnderFull()
    {
        WeatherRules r = Rules();
        r.virgaStrength = 1f;
        Assert.AreEqual(0.7f, WeatherMath.GroundRain(0.7f, 0f, 1f, r), 1e-6f);
        Assert.AreEqual(0.7f, WeatherMath.GroundRain(0.7f, 1f, 0f, r), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.GroundRain(0.7f, 1f, 1f, r), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.GroundRain(0.7f, 1f, 1f, 5f), 1e-6f);
        Assert.Less(WeatherMath.GroundRain(0.7f, 0.8f, 0.8f, r), WeatherMath.GroundRain(0.7f, 0.3f, 0.8f, r));
    }

    [Test]
    public void PressureIsNegativeAndDeepestAtTheCentre()
    {
        Assert.AreEqual(-5f, WeatherMath.Pressure(0f, 100f, 5f), 1e-6f);
        Assert.Less(WeatherMath.Pressure(50f, 100f, 5f), 0f);
        Assert.Greater(WeatherMath.Pressure(50f, 100f, 5f), WeatherMath.Pressure(0f, 100f, 5f));
        Assert.Greater(WeatherMath.Pressure(300f, 100f, 5f), -1e-3f);
    }

    [Test]
    public void GustFrontIsZeroAtTheCentreOutwardAndStrongestNearTheRadius()
    {
        Assert.AreEqual(0f, math.length(WeatherMath.GustFront(float2.zero, 100f, 2f)), 1e-6f);

        float2 east = WeatherMath.GustFront(new float2(100f, 0f), 100f, 2f);
        Assert.Greater(east.x, 0f);
        Assert.AreEqual(0f, east.y, 1e-5f);
        Assert.AreEqual(2f, math.length(east), 1e-4f);

        float2 west = WeatherMath.GustFront(new float2(-100f, 0f), 100f, 2f);
        Assert.AreEqual(-east.x, west.x, 1e-5f);

        float inner = math.length(WeatherMath.GustFront(new float2(50f, 0f), 100f, 2f));
        float edge = math.length(east);
        float outer = math.length(WeatherMath.GustFront(new float2(180f, 0f), 100f, 2f));
        Assert.Less(inner, edge);
        Assert.Less(outer, edge);
        Assert.AreEqual(0f, math.length(WeatherMath.GustFront(new float2(WeatherMath.GustReach * 100f, 0f), 100f, 2f)), 1e-5f);

        float2 diagonal = WeatherMath.GustFront(new float2(70f, 70f), 100f, 2f);
        Assert.AreEqual(diagonal.x, diagonal.y, 1e-5f);
        Assert.Greater(diagonal.x, 0f);
    }

    [Test]
    public void WetnessGrowsUnderRainAndDriesToZeroWithout()
    {
        WeatherRules r = Rules();
        float wet = 0f;
        for (int i = 0; i < 40; i++) wet = WeatherMath.WetnessStep(wet, 1f, 0.2f, 0f, 0.5f, r);
        Assert.Greater(wet, 0.3f);
        Assert.LessOrEqual(wet, 1f);

        float peak = wet;
        for (int i = 0; i < 4000; i++) wet = WeatherMath.WetnessStep(wet, 0f, 0.2f, 0f, 0.5f, r);
        Assert.Less(wet, peak);
        Assert.AreEqual(0f, wet, 1e-6f);
    }

    [Test]
    public void HeatAndWindDryTheGroundFaster()
    {
        WeatherRules r = Rules();
        float calm = WeatherMath.WetnessStep(0.8f, 0f, 0f, 0f, 1f, r);
        Assert.Less(WeatherMath.WetnessStep(0.8f, 0f, 1f, 0f, 1f, r), calm);
        Assert.Less(WeatherMath.WetnessStep(0.8f, 0f, 0f, 1f, 1f, r), calm);
    }

    [Test]
    public void StrikeRateStartsAboveTheThreshold()
    {
        WeatherRules r = Rules();
        Assert.AreEqual(0f, WeatherMath.StrikeRate(0f, r), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.StrikeRate(r.strikeThreshold, r), 1e-6f);
        Assert.Greater(WeatherMath.StrikeRate(r.strikeThreshold + 0.1f, r), 0f);
        Assert.Greater(WeatherMath.StrikeRate(1f, r), WeatherMath.StrikeRate(0.5f, r));
        Assert.AreEqual(r.strikesPerSecond * (1f - r.strikeThreshold), WeatherMath.StrikeRate(1f, r), 1e-5f);
    }

    [Test]
    public void StrikeDamageIsTheSameForAnyCreatureAtTheSamePoint()
    {
        WeatherRules r = Rules();
        float atPoint = WeatherMath.StrikeDamage(0f, r.creatureStrikeRadius, r.strikeDamage, 1f);
        Assert.AreEqual(r.strikeDamage, atPoint, 1e-4f);
        Assert.AreEqual(atPoint, WeatherMath.StrikeDamage(0f, r.creatureStrikeRadius, r.strikeDamage, 1f), 0f);
        Assert.AreEqual(0f, WeatherMath.StrikeDamage(r.creatureStrikeRadius, r.creatureStrikeRadius, r.strikeDamage, 1f), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.StrikeDamage(r.creatureStrikeRadius * 2f, r.creatureStrikeRadius, r.strikeDamage, 1f), 1e-6f);
        Assert.AreEqual(atPoint * 0.5f, WeatherMath.StrikeDamage(0f, r.creatureStrikeRadius, r.strikeDamage, 0.5f), 1e-4f);
        Assert.Greater(WeatherMath.StrikeDamage(0.5f, r.creatureStrikeRadius, r.strikeDamage, 1f),
            WeatherMath.StrikeDamage(2f, r.creatureStrikeRadius, r.strikeDamage, 1f));
    }

    [Test]
    public void GroundThresholdIsNotBelowTheStrikeThreshold()
    {
        WeatherRules r = Rules();
        r.strikeThreshold = 0.6f;
        r.groundThreshold = 0.3f;
        r.Validate();
        Assert.GreaterOrEqual(r.groundThreshold, r.strikeThreshold);
        Assert.AreEqual(0f, WeatherMath.StrikeRate(r.strikeThreshold, r), 1e-6f);
    }

    [Test]
    public void ReturnStrokesFlashThenFadeWithinTheDuration()
    {
        const float duration = 0.3f;
        Assert.AreEqual(1f, WeatherMath.StrokeIntensity(0f, 0.5f, duration), 1e-4f);
        Assert.AreEqual(0f, WeatherMath.StrokeIntensity(-0.01f, 0.5f, duration), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.StrokeIntensity(duration, 0.5f, duration), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.StrokeIntensity(duration * 2f, 0.5f, duration), 1e-6f);
        Assert.AreEqual(0f, WeatherMath.StrokeIntensity(0.1f, 0.5f, 0f), 1e-6f);

        float second = (WeatherMath.SecondStroke + 0.05f) * duration;
        float between = 0.5f * (WeatherMath.SecondStroke * duration);
        Assert.Greater(WeatherMath.StrokeIntensity(second, 0.5f, duration), 0.6f);
        Assert.Less(WeatherMath.StrokeIntensity(between, 0.5f, duration), WeatherMath.StrokeIntensity(second, 0.5f, duration));

        for (float t = 0f; t < duration; t += 0.005f)
        {
            float v = WeatherMath.StrokeIntensity(t, 0.3f, duration);
            Assert.GreaterOrEqual(v, 0f);
            Assert.LessOrEqual(v, 1f);
        }
    }
}
