using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class SurveySystemTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = TestRules.Bind();

    [TearDown]
    public void TearDown() => TestRules.Release(_rules);

    private static SurveyInput Query(EntityModel self, NativeArray<float3> positions, float nearest, float crowd, int cap,
        EntityModel victim = null, float stickMul = 1f, float lowHpMul = 1f, EntityModel familiar = null, float familiarMul = 1f)
        => new SurveyInput
        {
            Position      = positions[self.ManagerIndex],
            NearestRadius = nearest,
            CrowdRadius   = crowd,
            ColonyIndex   = self.Colony != null ? self.Colony.Index : -1,
            CrowdCap      = cap,
            SelfSlot      = self.GridSlot,
            VictimSlot    = victim != null ? victim.GridSlot : -1,
            FamiliarSlot  = familiar != null ? familiar.GridSlot : -1,
            StickMul      = stickMul,
            LowHpMul      = lowHpMul,
            FamiliarMul   = familiarMul,
        };

    private static void SetStats(EntityModel model, float hp, float max)
    {
        var stats = new StatsComponent();
        stats.FillComponent(max, max);
        stats.Hp = hp;
        typeof(EntityModel).GetProperty("Stats").GetSetMethod(true).Invoke(model, new object[] { stats });
    }

    [Test]
    public void JobMatchesManagedSurveyOnRandomPopulations()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            int n = 40 + seed * 37;
            var registry  = new ColonyRegistry();
            var grid      = new EntitySpatialGrid();
            var system    = new SurveySystem(n);
            var positions = new NativeArray<float3>(n, Allocator.Persistent);
            var models    = new EntityModel[n];
            try
            {
                var colonies = new[]
                {
                    registry.Add(new float2(0f, 0f), null, n),
                    registry.Add(new float2(500f, 0f), null, n),
                    registry.Add(new float2(0f, 500f), null, n),
                };
                var rng = new System.Random(seed * 101);

                for (int i = 0; i < n; i++)
                {
                    positions[i] = new float3((float)rng.NextDouble() * 600f, (float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 600f);
                    models[i] = new EntityModel { ManagerIndex = i };
                    int pick = rng.Next(4);
                    if (pick < 3) EntitySpatialGridSurveyTests.SetColony(models[i], colonies[pick]);
                    if (rng.Next(5) != 0) SetStats(models[i], 1f + (float)rng.NextDouble() * 99f, 100f);
                }

                grid.Rebuild(models, n, 100f, positions);

                for (int i = 0; i < n; i += 7)
                {
                    models[i].MarkDeathPending();
                    grid.NoteDeath(models[i]);
                }

                var radii = new float[n];
                var crowdRadii = new float[n];
                var victims = new EntityModel[n];
                var familiars = new EntityModel[n];
                var sticks = new float[n];
                var lowHp = new float[n];
                var familiarMul = new float[n];
                for (int i = 0; i < n; i++)
                {
                    if (models[i].IsDeathPending) continue;
                    radii[i]      = 20f + (float)rng.NextDouble() * 150f;
                    crowdRadii[i] = 5f + (float)rng.NextDouble() * 60f;
                    victims[i]    = rng.Next(3) == 0 ? models[rng.Next(n)] : null;
                    familiars[i]  = rng.Next(3) == 0 ? models[rng.Next(n)] : null;
                    if (familiars[i] != null && familiars[i].Stats == null) familiars[i] = null;
                    sticks[i]     = 0.4f + (float)rng.NextDouble();
                    lowHp[i]      = 0.5f + (float)rng.NextDouble() * 0.5f;
                    familiarMul[i] = 0.5f + (float)rng.NextDouble();
                    system.Write(i, Query(models[i], positions, radii[i], crowdRadii[i], 4096,
                        victims[i], sticks[i], lowHp[i], familiars[i], familiarMul[i]));
                }

                system.Schedule(grid, 0, n).Complete();

                for (int i = 0; i < n; i++)
                {
                    if (models[i].IsDeathPending) continue;
                    var self = models[i];
                    var o = system.Output(i);

                    var nearest = grid.Survey((Vector3)positions[i], self, self.Colony, radii[i], crowdRadii[i], 4096,
                        out float nearestDistance, out int crowd, out int kin, out var foreign, out float foreignDistance,
                        victims[i], sticks[i], lowHp[i], familiars[i]?.Stats, familiarMul[i]);

                    string what = $"seed {seed} agent {i}";
                    Assert.AreSame(nearest, grid.ModelAt(o.NearestSlot), what);
                    Assert.AreSame(foreign, grid.ModelAt(o.ForeignSlot), what);
                    Assert.AreEqual(nearestDistance, o.NearestDistance, 1e-3f, what);
                    Assert.AreEqual(foreignDistance, o.ForeignDistance, 1e-3f, what);
                    Assert.AreEqual(crowd, o.Crowd, what);
                    Assert.AreEqual(kin, o.Kin, what);
                }
            }
            finally
            {
                for (int i = 0; i < n; i++) models[i]?.Dispose();
                positions.Dispose();
                system.Dispose();
                grid.Dispose();
                registry.Dispose();
            }
        }
    }

    [Test]
    public void KinNearCountsSameColonyWithinItsOwnRadius()
    {
        const int n = 80;
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var system    = new SurveySystem(n);
        var positions = new NativeArray<float3>(n, Allocator.Persistent);
        var models    = new EntityModel[n];
        try
        {
            var colonies = new[] { registry.Add(new float2(0f, 0f), null, n), registry.Add(new float2(300f, 0f), null, n) };
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                positions[i] = new float3((float)rng.NextDouble() * 120f, 0f, (float)rng.NextDouble() * 120f);
                models[i] = new EntityModel { ManagerIndex = i };
                EntitySpatialGridSurveyTests.SetColony(models[i], colonies[i % 2]);
                SetStats(models[i], 50f, 100f);
            }
            grid.Rebuild(models, n, 100f, positions);

            var radii = new float[n];
            for (int i = 0; i < n; i++)
            {
                radii[i] = i % 5 == 0 ? 0f : 4f + (float)rng.NextDouble() * 40f;
                var q = Query(models[i], positions, 10f, 4f, 4096);
                q.KinRadius = radii[i];
                system.Write(i, q);
            }
            system.Schedule(grid, 0, n).Complete();

            for (int i = 0; i < n; i++)
            {
                int expected = 0;
                for (int j = 0; j < n && radii[i] > 0f; j++)
                    if (j != i && j % 2 == i % 2 && math.distancesq(positions[i], positions[j]) <= radii[i] * radii[i]) expected++;
                Assert.AreEqual(expected, system.Output(i).KinNear, $"agent {i}");
            }
        }
        finally
        {
            for (int i = 0; i < n; i++) models[i]?.Dispose();
            positions.Dispose();
            system.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    [Test]
    public void EmptyGridAndSingleAgentGiveNoNeighbours()
    {
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var system    = new SurveySystem(2);
        var positions = new NativeArray<float3>(1, Allocator.Persistent);
        var models    = new EntityModel[1];
        try
        {
            models[0] = new EntityModel { ManagerIndex = 0 };
            positions[0] = float3.zero;

            system.Write(0, Query(models[0], positions, 50f, 10f, 16));
            system.Schedule(grid, 0, 1).Complete();
            Assert.AreEqual(-1, system.Output(0).NearestSlot);
            Assert.AreEqual(-1f, system.Output(0).NearestDistance);

            grid.Rebuild(models, 1, 100f, positions);
            system.Write(0, Query(models[0], positions, 50f, 10f, 16));
            system.Schedule(grid, 0, 1).Complete();
            var o = system.Output(0);
            Assert.AreEqual(-1, o.NearestSlot);
            Assert.AreEqual(-1, o.ForeignSlot);
            Assert.AreEqual(0, o.Crowd);
            Assert.AreEqual(0, o.Kin);
        }
        finally
        {
            models[0]?.Dispose();
            positions.Dispose();
            system.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    [Test]
    public void NeighbourExactlyOnTheRadiusIsIncludedAndOneBeyondIsNot()
    {
        var registry  = new ColonyRegistry();
        var grid      = new EntitySpatialGrid();
        var system    = new SurveySystem(3);
        var positions = new NativeArray<float3>(3, Allocator.Persistent);
        var models    = new EntityModel[3];
        try
        {
            var a = registry.Add(new float2(0f, 0f), null, 3);
            positions[0] = new float3(0f, 0f, 0f);
            positions[1] = new float3(10f, 0f, 0f);
            positions[2] = new float3(10.01f, 0f, 0f);
            for (int i = 0; i < 3; i++)
            {
                models[i] = new EntityModel { ManagerIndex = i };
                EntitySpatialGridSurveyTests.SetColony(models[i], a);
            }

            grid.Rebuild(models, 3, 100f, positions);
            system.Write(0, Query(models[0], positions, 10f, 10f, 16));
            system.Schedule(grid, 0, 1).Complete();

            var o = system.Output(0);
            Assert.AreEqual(models[1].GridSlot, o.NearestSlot);
            Assert.AreEqual(10f, o.NearestDistance, 1e-4f);
            Assert.AreEqual(1, o.Crowd);
            Assert.AreEqual(1, o.Kin);
        }
        finally
        {
            for (int i = 0; i < 3; i++) models[i]?.Dispose();
            positions.Dispose();
            system.Dispose();
            grid.Dispose();
            registry.Dispose();
        }
    }

    [Test]
    public void WriteAndOutputDoNotAllocate()
    {
        var system = new SurveySystem(8);
        try
        {
            var input = new SurveyInput
            {
                Position = new float3(1f, 2f, 3f), NearestRadius = 10f, CrowdRadius = 5f, CrowdCap = 16,
                SelfSlot = -1, VictimSlot = -1, FamiliarSlot = -1, StickMul = 1f, LowHpMul = 1f, FamiliarMul = 1f
            };
            int sink = 0;
            AllocationAssert.Zero(() =>
            {
                system.Write(3, in input);
                sink += system.Output(3).Crowd;
            }, 256, "SurveySystem.Write/Output");
            Assert.AreEqual(0, sink);
        }
        finally
        {
            system.Dispose();
        }
    }
}
