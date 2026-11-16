using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public class SimQualityTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp()
    {
        SimulationClock.Reset();
        _rules = TestRules.Bind();
    }

    [TearDown]
    public void TearDown()
    {
        TestRules.Release(_rules);
        SimulationClock.Reset();
    }

    [Test]
    public void StageDefaultsAreSet()
    {
        var r = _rules;
        Assert.AreEqual(0.7f, r.DietPlantFloor);
        Assert.AreEqual(0.7f, r.DietMeatFloor);
        Assert.AreEqual(0.85f, r.DigestEffActive);
        Assert.AreEqual(2.5f, r.DigestRateActive);
        Assert.AreEqual(12f, r.LevyMinStep);
        Assert.AreEqual(1.2f, r.LevyAlpha);
        Assert.AreEqual(60f, r.PlannerRadiusMax);
        Assert.AreEqual(40f, r.FrontierRadius);
        Assert.AreEqual(5, r.PatrolLegs);
        Assert.AreEqual(120f, r.PlanMaxTravel);
        Assert.AreEqual(30f, r.FleeSprintDistance);
        Assert.AreEqual(1.6f, r.ScoutSpeedMul);
        Assert.AreEqual(1.6f, r.ScoutEnergyCapMul);
        Assert.AreEqual(5f, r.AmbushMaxHold);
        Assert.AreEqual(1.5f, r.MigrationDangerWeight);
        Assert.AreEqual(1.5f, r.ScoutRangeMul);
        Assert.AreEqual(0.5f, r.HuntStickMul);
        Assert.AreEqual(0.5f, r.HuntLowHpMul);
        Assert.AreEqual(0.25f, r.HuntFinishHp);
        Assert.AreEqual(0.3f, r.OutnumberedStress);
        Assert.AreEqual(3f, r.OutnumberedNorm);
        Assert.AreEqual(12, r.FireSenseRadius);
        Assert.AreEqual(0.6f, r.FireStress);
        Assert.AreEqual(3f, r.FireDangerMark);
    }

    [Test]
    public void ScoutRangeIsAMultiplierOfTheNeutralCaste()
    {
        Assert.AreEqual(1f, CasteModifiers.Neutral.Range);
        Assert.AreEqual(_rules.ScoutRangeMul, CasteModifiers.For(Caste.Scout).Range);
        Assert.AreEqual(1f, CasteModifiers.For(Caste.Worker).Range);
    }

    [Test]
    public void BoardValueFallsByAlphaPerEmptyReport()
    {
        var board = new ColonyPoiBoard(4);
        var at = new float2(10f, 10f);
        board.Offer(at, 20f, 0f);
        float alpha = _rules.PoiEnergyEmaAlpha;

        for (int n = 1; n <= 5; n++)
        {
            board.ReportEmpty(at);
            Assert.AreEqual(20f * math.pow(1f - alpha, n), board.At(0).EnergyEma, 1e-4f, $"n = {n}");
        }

        board.ReportEmpty(new float2(1000f, 1000f));
        Assert.AreEqual(1, board.Count);
        Assert.AreEqual(20f * math.pow(1f - alpha, 5), board.At(0).EnergyEma, 1e-4f);
    }

    [Test]
    public void EmptyReportLowersTheOwnPointAndDoesNotCreateOne()
    {
        var points = new PointsOfInterestComponent();
        var at = new float2(0f, 0f);
        Assert.IsTrue(points.TryRemember(in at, 1f));
        for (int i = 0; i < 10; i++) points.CreditEnergy(in at, 20f);
        float before = points.At(0).EnergyEma;

        points.ReportEmpty(in at);
        Assert.AreEqual(before * (1f - _rules.PoiEnergyEmaAlpha), points.At(0).EnergyEma, 1e-4f);

        var far = new float2(500f, 500f);
        points.ReportEmpty(in far);
        Assert.AreEqual(1, points.Count);
    }

    [Test]
    public void NegativeMarkOfOneOutweighsOneFoodPortion()
    {
        var points = new PointsOfInterestComponent();
        var a = new float2(100f, 0f);
        var b = new float2(-100f, 0f);
        Assert.IsTrue(points.TryRemember(in a, 1f));
        Assert.IsTrue(points.TryRemember(in b, 1f));
        for (int i = 0; i < 10; i++) points.CreditEnergy(in a, _rules.EatEnergyFood);

        var self = float2.zero;
        Assert.IsTrue(points.TryPickBest(in self, out float2 pick));
        Assert.AreEqual(a.x, pick.x);

        points.MarkNegative(in a, 1f);
        Assert.IsTrue(points.TryPickBest(in self, out pick));
        Assert.AreEqual(b.x, pick.x);
    }

    private static NativeHashMap<long, FireCell> Burning(params int2[] cells)
    {
        var map = new NativeHashMap<long, FireCell>(16, Allocator.Persistent);
        for (int i = 0; i < cells.Length; i++)
            map[Position2Int.Pack(cells[i].x, cells[i].y)] = new FireCell { Left = 1f, Total = 1f, Size = 1f };
        return map;
    }

    [Test]
    public void FireQueryFindsTheNearestBurningCellWithinRadius()
    {
        var map = Burning(new int2(3, 0), new int2(0, 5), new int2(-20, 0));
        try
        {
            var p = FireEvent.CellCenter(Position2Int.Pack(0, 0));
            Assert.IsTrue(FireEvent.FindNearestBurning(map.AsReadOnly(), p, 10, out float2 center, out float distance));
            var expected = FireEvent.CellCenter(Position2Int.Pack(3, 0));
            Assert.AreEqual(expected.x, center.x, 1e-4f);
            Assert.AreEqual(expected.y, center.y, 1e-4f);
            Assert.AreEqual(math.distance(p, expected), distance, 1e-4f);

            Assert.IsFalse(FireEvent.FindNearestBurning(map.AsReadOnly(), p, 2, out _, out float none));
            Assert.AreEqual(-1f, none);
            Assert.IsFalse(FireEvent.FindNearestBurning(map.AsReadOnly(), p, 0, out _, out _));
        }
        finally
        {
            map.Dispose();
        }
    }

    [Test]
    public void FireQueryDoesNotAllocate()
    {
        var map = Burning(new int2(3, 0), new int2(0, 5));
        try
        {
            var readOnly = map.AsReadOnly();
            var p = FireEvent.CellCenter(Position2Int.Pack(0, 0));
            float sink = 0f;
            TestDelegate step = () =>
            {
                FireEvent.FindNearestBurning(readOnly, p, 12, out float2 c, out float d);
                sink += c.x + d;
            };
            AllocationAssert.Zero(step, 200, "FireEvent.FindNearestBurning");
        }
        finally
        {
            map.Dispose();
        }
    }

    private static void SetColony(EntityModel model, ColonyState colony)
        => typeof(EntityModel).GetProperty("Colony").GetSetMethod(true).Invoke(model, new object[] { colony });

    [Test]
    public void SurveyPrefersTheLastVictimAndDoesNotAllocate()
    {
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var positions = new NativeArray<float3>(3, Allocator.Persistent);
        var models    = new EntityModel[3];
        try
        {
            var own     = registry.Add(new float2(0f, 0f), null, 3);
            var foreign = registry.Add(new float2(500f, 0f), null, 3);
            positions[0] = new float3(0f, 0f, 0f);
            positions[1] = new float3(10f, 0f, 0f);
            positions[2] = new float3(0f, 0f, 6f);
            for (int i = 0; i < 3; i++)
            {
                models[i] = new EntityModel { ManagerIndex = i };
                SetColony(models[i], i == 0 ? own : foreign);
            }
            grid.Rebuild(models, 3, 100f, positions);

            var origin = (Vector3)positions[0];
            grid.Survey(origin, models[0], own, 50f, 20f, 16, out _, out _, out _, out var plain, out float plainDistance);
            Assert.AreSame(models[2], plain);
            Assert.AreEqual(6f, plainDistance, 1e-3f);

            grid.Survey(origin, models[0], own, 50f, 20f, 16, out _, out _, out _, out var sticky, out float stickyDistance,
                models[1], 0.5f, 0.5f);
            Assert.AreSame(models[1], sticky);
            Assert.AreEqual(10f, stickyDistance, 1e-3f);

            var victim = models[1];
            TestDelegate step = () => grid.Survey(origin, models[0], own, 50f, 20f, 16, out _, out _, out _, out _, out _,
                victim, 0.5f, 0.5f);
            AllocationAssert.Zero(step, 200, "EntitySpatialGrid.Survey with target scoring");
        }
        finally
        {
            for (int i = 0; i < 3; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }
}
