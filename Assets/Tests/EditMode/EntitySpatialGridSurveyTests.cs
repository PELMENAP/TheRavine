using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class EntitySpatialGridSurveyTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = TestRules.Bind();

    [TearDown]
    public void TearDown() => TestRules.Release(_rules);

    internal static void SetColony(EntityModel model, ColonyState colony)
        => typeof(EntityModel).GetProperty("Colony").GetSetMethod(true).Invoke(model, new object[] { colony });

    [Test]
    public void NearestForeignMatchesBruteForceWithSixtyFourAgentsInOneCell()
    {
        const int n = 64;
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var positions = new NativeArray<float3>(n, Allocator.Persistent);
        var models    = new EntityModel[n];
        try
        {
            var a = registry.Add(new float2(0f, 0f), null, n);
            var b = registry.Add(new float2(500f, 0f), null, n);
            var rng = new System.Random(7);

            for (int i = 0; i < n; i++)
            {
                positions[i] = new float3((float)rng.NextDouble() * 80f, (float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 80f);
                models[i] = new EntityModel { ManagerIndex = i };
                SetColony(models[i], i % 4 == 0 ? b : a);
            }

            grid.Rebuild(models, n, 100f, positions);

            for (int i = 0; i < n; i++)
            {
                var self = models[i];
                var origin = (Vector3)positions[i];
                grid.Survey(origin, self, self.Colony, 200f, 20f, 16,
                    out _, out int crowd, out int kin, out var foreign, out float foreignDistance);

                int expected = -1;
                float best = float.MaxValue;
                int expectedKin = 0, expectedCrowd = 0;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    float d2 = math.distancesq(positions[i], positions[j]);
                    if (models[j].Colony != self.Colony && d2 < best) { best = d2; expected = j; }
                    if (d2 <= 400f && expectedCrowd < 16)
                    {
                        expectedCrowd++;
                        if (models[j].Colony == self.Colony) expectedKin++;
                    }
                }

                Assert.AreSame(models[expected], foreign, $"agent {i}");
                Assert.AreEqual(math.sqrt(best), foreignDistance, 1e-3f);
                Assert.AreEqual(expectedCrowd, crowd);
                Assert.AreEqual(expectedKin, kin);
            }
        }
        finally
        {
            for (int i = 0; i < n; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    [Test]
    public void SurveyReportsNoForeignWhenOnlyOwnColonyIsPresent()
    {
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var positions = new NativeArray<float3>(2, Allocator.Persistent);
        var models    = new EntityModel[2];
        try
        {
            var a = registry.Add(new float2(0f, 0f), null, 2);
            for (int i = 0; i < 2; i++)
            {
                positions[i] = new float3(i, 0f, 0f);
                models[i] = new EntityModel { ManagerIndex = i };
                SetColony(models[i], a);
            }

            grid.Rebuild(models, 2, 100f, positions);
            grid.Survey((Vector3)positions[0], models[0], a, 50f, 10f, 16, out _, out _, out int kin, out var foreign, out float distance);

            Assert.IsNull(foreign);
            Assert.AreEqual(-1f, distance);
            Assert.AreEqual(1, kin);
        }
        finally
        {
            for (int i = 0; i < 2; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    [Test]
    public void AgentWithoutColonyIsForeignToFirstColony()
    {
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var positions = new NativeArray<float3>(2, Allocator.Persistent);
        var models    = new EntityModel[2];
        try
        {
            var a = registry.Add(new float2(0f, 0f), null, 2);
            positions[0] = new float3(0f, 0f, 0f);
            positions[1] = new float3(3f, 0f, 0f);
            models[0] = new EntityModel { ManagerIndex = 0 };
            models[1] = new EntityModel { ManagerIndex = 1 };
            SetColony(models[0], a);

            grid.Rebuild(models, 2, 100f, positions);

            grid.Survey((Vector3)positions[0], models[0], a, 50f, 10f, 16, out _, out _, out int kin, out var foreign, out float distance);
            Assert.AreSame(models[1], foreign);
            Assert.AreEqual(3f, distance, 1e-3f);
            Assert.AreEqual(0, kin);

            grid.Survey((Vector3)positions[1], models[1], null, 50f, 10f, 16, out _, out _, out kin, out foreign, out distance);
            Assert.AreSame(models[0], foreign);
            Assert.AreEqual(0, kin);
        }
        finally
        {
            for (int i = 0; i < 2; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    private static EntityModel[] Line(ColonyRegistry registry, int n, out NativeArray<float3> positions, out ColonyState colony)
    {
        colony = registry.Add(new float2(0f, 0f), null, n);
        positions = new NativeArray<float3>(n, Allocator.Persistent);
        var models = new EntityModel[n];
        for (int i = 0; i < n; i++)
        {
            positions[i] = new float3(i * 5f, 0f, 0f);
            models[i] = new EntityModel { ManagerIndex = i };
            SetColony(models[i], colony);
        }
        return models;
    }

    [Test]
    public void NeighbourThatDiesInsideTheWindowIsSkipped()
    {
        const int n = 4;
        var registry = new ColonyRegistry();
        var grid     = new EntitySpatialGrid();
        var models   = Line(registry, n, out var positions, out var colony);
        var found    = new EntityModel[n];
        try
        {
            grid.Rebuild(models, n, 100f, positions);
            var origin = (Vector3)positions[0];

            var nearest = grid.Survey(origin, models[0], colony, 50f, 12f, 16, out float distance, out int crowd, out _, out _, out _);
            Assert.AreSame(models[1], nearest);
            Assert.AreEqual(5f, distance, 1e-3f);
            Assert.AreEqual(2, crowd);

            models[1].MarkDeathPending();
            grid.NoteDeath(models[1]);

            nearest = grid.Survey(origin, models[0], colony, 50f, 12f, 16, out distance, out crowd, out int kin, out _, out _);
            Assert.AreSame(models[2], nearest);
            Assert.AreEqual(10f, distance, 1e-3f);
            Assert.AreEqual(1, crowd);
            Assert.AreEqual(1, kin);

            Assert.AreSame(models[2], grid.FindNearest(origin, models[0], 50f, out distance));
            int count = grid.FindInRadius(origin, models[0], 50f, found);
            Assert.AreEqual(2, count);
            Assert.IsFalse(System.Array.IndexOf(found, models[1], 0, count) >= 0);
        }
        finally
        {
            for (int i = 0; i < n; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    [Test]
    public void DamagedNeighbourKeepsItsSlotAndUpdatesTheHpFraction()
    {
        const int n = 3;
        var registry = new ColonyRegistry();
        var grid     = new EntitySpatialGrid();
        var models   = Line(registry, n, out var positions, out var colony);
        try
        {
            grid.Rebuild(models, n, 100f, positions);
            Assert.AreEqual(1f, grid.HpFractionOf(models[1]), 1e-6f);

            grid.NoteHp(models[1], 25f, 100f);
            Assert.AreEqual(0.25f, grid.HpFractionOf(models[1]), 1e-6f);
            Assert.AreEqual(1f, grid.HpFractionOf(models[2]), 1e-6f);

            grid.NoteHp(models[1], -10f, 100f);
            Assert.AreEqual(0f, grid.HpFractionOf(models[1]), 1e-6f);

            var nearest = grid.Survey((Vector3)positions[0], models[0], colony, 50f, 12f, 16, out float distance, out int crowd, out _, out _, out _);
            Assert.AreSame(models[1], nearest);
            Assert.AreEqual(5f, distance, 1e-3f);
            Assert.AreEqual(2, crowd);
        }
        finally
        {
            for (int i = 0; i < n; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    [Test]
    public void ModelExcludedAtRebuildLosesItsGridSlot()
    {
        const int n = 3;
        var registry = new ColonyRegistry();
        var grid     = new EntitySpatialGrid();
        var models   = Line(registry, n, out var positions, out _);
        try
        {
            grid.Rebuild(models, n, 100f, positions);
            Assert.AreEqual(1, models[1].GridSlot);

            models[1].MarkDeathPending();
            grid.Rebuild(models, n, 100f, positions);
            Assert.AreEqual(-1, models[1].GridSlot);
            Assert.AreEqual(2, grid.Count);
            Assert.AreEqual(1, models[2].GridSlot);
        }
        finally
        {
            for (int i = 0; i < n; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }
}
