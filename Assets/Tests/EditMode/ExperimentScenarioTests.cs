using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public class ExperimentScenarioTests
{
    private ExperimentScenario _scenario;

    [SetUp]
    public void SetUp() => _scenario = ScriptableObject.CreateInstance<ExperimentScenario>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_scenario);

    [Test]
    public void DefaultScenarioHasTwoIdenticalColonies()
    {
        Assert.AreEqual(2, _scenario.ColonyCount);
        var a = _scenario.ColonyAt(0);
        var b = _scenario.ColonyAt(1);
        Assert.AreEqual(a.foodMultiplier, b.foodMultiplier);
        Assert.AreEqual(a.foodRadius, b.foodRadius);
        Assert.AreEqual(a.foodActiveSeconds, b.foodActiveSeconds);
        Assert.AreNotEqual(a.nestOffset, b.nestOffset);
    }

    [Test]
    public void NeutralColoniesBuildNoFoodZones()
    {
        var zones = new FoodZone[ExperimentScenario.MaxColonies];
        Assert.AreEqual(0, _scenario.BuildZones(Vector3.zero, zones));
    }

    [Test]
    public void ModifiedColonyBuildsAZoneAroundItsNest()
    {
        _scenario.ColonyAt(1).foodMultiplier = 0.25f;
        var zones = new FoodZone[ExperimentScenario.MaxColonies];
        var origin = new Vector3(10f, 0f, 20f);

        int count = _scenario.BuildZones(origin, zones);
        Assert.AreEqual(1, count);

        float2 nest = _scenario.NestPosition(1, origin);
        Assert.AreEqual(0.25f, FoodZone.MultiplierAt(zones, count, nest, 0d));
        Assert.AreEqual(0.25f, FoodZone.MultiplierAt(zones, count, nest + new float2(100f, 0f), 0d));
        Assert.AreEqual(1f, FoodZone.MultiplierAt(zones, count, nest + new float2(500f, 0f), 0d));
        Assert.AreEqual(1f, FoodZone.MultiplierAt(zones, count, _scenario.NestPosition(0, origin), 0d));
    }

    [Test]
    public void SeasonalWindowSwitchesTheModifierOnAndOff()
    {
        _scenario.ColonyAt(0).foodMultiplier = 2f;
        _scenario.ColonyAt(0).foodStartSeconds = 100f;
        _scenario.ColonyAt(0).foodActiveSeconds = 50f;
        _scenario.ColonyAt(0).foodInactiveSeconds = 150f;
        var zones = new FoodZone[ExperimentScenario.MaxColonies];
        int count = _scenario.BuildZones(Vector3.zero, zones);
        float2 nest = _scenario.NestPosition(0, Vector3.zero);

        Assert.AreEqual(1f, FoodZone.MultiplierAt(zones, count, nest, 50d));
        Assert.AreEqual(2f, FoodZone.MultiplierAt(zones, count, nest, 120d));
        Assert.AreEqual(1f, FoodZone.MultiplierAt(zones, count, nest, 200d));
        Assert.AreEqual(2f, FoodZone.MultiplierAt(zones, count, nest, 320d));
    }

    [Test]
    public void ZoneLookupDoesNotAllocate()
    {
        _scenario.ColonyAt(0).foodMultiplier = 0.5f;
        var zones = new FoodZone[ExperimentScenario.MaxColonies];
        int count = _scenario.BuildZones(Vector3.zero, zones);
        float sink = 0f;
        TestDelegate step = () => sink += FoodZone.MultiplierAt(zones, count, new float2(-150f, 0f), 10d);
        AllocationAssert.Zero(step, 200, "FoodZone.MultiplierAt");
    }
}
