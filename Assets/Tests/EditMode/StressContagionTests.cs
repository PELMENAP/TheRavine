using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using TheRavine.EntityControl;

public class StressContagionTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = ScriptableObject.CreateInstance<SimulationRules>();

    [TearDown]
    public void TearDown()
    {
        SimulationRules.Bind(null);
        Object.DestroyImmediate(_rules);
    }

    private void Bind(bool sources, bool contagion, bool player = false)
    {
        var so = new SerializedObject(_rules);
        so.FindProperty("stressSourcesEnabled").boolValue = sources;
        so.FindProperty("stressContagionEnabled").boolValue = contagion;
        so.FindProperty("playerStressEnabled").boolValue = player;
        so.ApplyModifiedPropertiesWithoutUndo();
        SimulationRules.Bind(_rules);
    }

    private static void SetLevel(EntityModel model, int level)
        => typeof(EntityModel).GetProperty("StressLevel").GetSetMethod(true).Invoke(model, new object[] { level });

    [Test]
    public void RateIsZeroWithoutNeighboursOrFlags()
    {
        Bind(true, true);
        ref readonly var r = ref SimulationRules.Frame;
        Assert.AreEqual(0f, CreatureCore.ContagionStressRate(0, 0, in r));
        Assert.AreEqual(0f, CreatureCore.ContagionStressRate(0, 5, in r));
        Assert.AreEqual(r.StressContagionRate, CreatureCore.ContagionStressRate(5, 5, in r), 1e-6f);
        Assert.AreEqual(r.StressContagionRate * 0.4f, CreatureCore.ContagionStressRate(2, 5, in r), 1e-6f);

        Bind(true, false);
        Assert.AreEqual(0f, CreatureCore.ContagionStressRate(5, 5, in SimulationRules.Frame));
        Bind(false, true);
        Assert.AreEqual(0f, CreatureCore.ContagionStressRate(5, 5, in SimulationRules.Frame));
    }

    [Test]
    public void GridCountMatchesSurveyJobAndBruteForce()
    {
        Bind(true, true);
        const int n = 120;
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var system    = new SurveySystem(n);
        var positions = new NativeArray<float3>(n, Allocator.Persistent);
        var models    = new EntityModel[n];
        try
        {
            var a = registry.Add(new float2(0f, 0f), null, n);
            var b = registry.Add(new float2(300f, 0f), null, n);
            var rng = new System.Random(11);
            for (int i = 0; i < n; i++)
            {
                positions[i] = new float3((float)rng.NextDouble() * 60f, 0f, (float)rng.NextDouble() * 60f);
                models[i] = new EntityModel { ManagerIndex = i };
                EntitySpatialGridSurveyTests.SetColony(models[i], i % 3 == 0 ? b : a);
                if (rng.Next(3) == 0) SetLevel(models[i], CreatureCore.StressHigh);
            }
            grid.Rebuild(models, n, 10f, positions);
            const float radius = 8f;

            for (int i = 0; i < n; i++)
                system.Write(i, new SurveyInput
                {
                    Position = positions[i], AlarmRadius = radius, ColonyIndex = models[i].Colony.Index,
                    SelfSlot = models[i].GridSlot, VictimSlot = -1, FamiliarSlot = -1, StickMul = 1f, LowHpMul = 1f, FamiliarMul = 1f,
                });
            system.Schedule(grid, 0, n).Complete();

            for (int i = 0; i < n; i++)
            {
                int near = 0, alarmed = 0;
                for (int j = 0; j < n; j++)
                {
                    if (j == i || math.distancesq(positions[i], positions[j]) > radius * radius) continue;
                    near++;
                    if (models[j].StressLevel == CreatureCore.StressHigh) alarmed++;
                }
                var output = system.Output(i);
                Assert.AreEqual(near, output.Near, $"near {i}");
                Assert.AreEqual(alarmed, output.AlarmNear, $"alarmed {i}");

                int gridAlarmed = grid.CountAlarmed(positions[i], radius, out int gridNear);
                bool selfAlarmed = models[i].StressLevel == CreatureCore.StressHigh;
                Assert.AreEqual(near + 1, gridNear, $"grid near {i}");
                Assert.AreEqual(alarmed + (selfAlarmed ? 1 : 0), gridAlarmed, $"grid alarmed {i}");
            }
        }
        finally
        {
            positions.Dispose();
            system.Dispose();
            grid.Dispose();
        }
    }

    [Test]
    public void CountAlarmedAllocatesNothing()
    {
        Bind(true, true);
        const int n = 32;
        var grid      = new EntitySpatialGrid();
        var positions = new NativeArray<float3>(n, Allocator.Persistent);
        var models    = new EntityModel[n];
        try
        {
            for (int i = 0; i < n; i++)
            {
                positions[i] = new float3(i, 0f, i % 5);
                models[i] = new EntityModel { ManagerIndex = i };
            }
            grid.Rebuild(models, n, 4f, positions);
            int sink = 0;
            AllocationAssert.Zero(() => sink += grid.CountAlarmed(new float3(10f, 0f, 2f), 6f, out int near) + near, 200,
                "EntitySpatialGrid.CountAlarmed");
        }
        finally
        {
            positions.Dispose();
            grid.Dispose();
        }
    }

    [Test]
    public void PlayerStressFollowsSharedRulesAndCalmsWithoutInput()
    {
        Bind(true, true, true);
        ref readonly var r = ref SimulationRules.Frame;
        var stress = new PlayerStress();
        for (int i = 0; i < 200; i++) stress.Step(1f, 1f, 1f, false, 4, 4, in r);
        Assert.Greater(stress.Stress, 0f);
        float peak = stress.Stress;
        for (int i = 0; i < 2000; i++) stress.Step(1f, 1f, 1f, false, 0, 0, in r);
        Assert.Less(stress.Stress, peak * 0.05f);
        Assert.AreEqual(CreatureCore.StressCalm, stress.Level);

        Bind(true, true, false);
        stress.Step(1f, 0f, 3f, true, 4, 4, in SimulationRules.Frame);
        Assert.AreEqual(0f, stress.Stress);
    }
}
