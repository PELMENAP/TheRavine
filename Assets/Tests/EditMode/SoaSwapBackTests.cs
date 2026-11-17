using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class SoaSwapBackTests
{
    [SetUp]
    public void SetUp() => SimulationRules.Bind(null);

    [Test]
    public void CasteThresholdsFollowSwapBack()
    {
        var system = new CasteThresholdSystem(4);
        try
        {
            for (int i = 0; i < 4; i++) system.Reset(i, new float4(i));
            system.RemoveSwapBack(1, 3);

            Assert.AreEqual(new float4(0f), system.Get(0));
            Assert.AreEqual(new float4(3f), system.Get(1));
            Assert.AreEqual(new float4(2f), system.Get(2));
            Assert.AreEqual(float4.zero, system.Get(3));

            system.RemoveSwapBack(2, 2);
            Assert.AreEqual(float4.zero, system.Get(2));
        }
        finally
        {
            system.Dispose();
        }
    }

    [Test]
    public void InstinctSlotIsResetAfterSwapBack()
    {
        var system = new InstinctSystem(4);
        try
        {
            for (int i = 0; i < 4; i++) system.Reset(i, default);
            system.RemoveSwapBack(0, 3);

            var freed = system.Output(3);
            Assert.AreEqual(InstinctBits.NoReflex, freed.Reflex);
            Assert.AreEqual(1, freed.NeedsDecision);
        }
        finally
        {
            system.Dispose();
        }
    }

    [Test]
    public void SurveyOutputsFollowSwapBack()
    {
        const int n = 4;
        var system    = new SurveySystem(n);
        var grid      = new EntitySpatialGrid();
        var positions = new NativeArray<float3>(n, Allocator.Persistent);
        var models    = new EntityModel[n];
        try
        {
            float[] xs = { 0f, 3f, 500f, 507f };
            for (int i = 0; i < n; i++)
            {
                positions[i] = new float3(xs[i], 0f, 0f);
                models[i] = new EntityModel { ManagerIndex = i };
            }

            grid.Rebuild(models, n, 100f, positions);
            for (int i = 0; i < n; i++)
            {
                system.Write(i, new SurveyInput
                {
                    Position = positions[i], NearestRadius = 20f, CrowdRadius = 20f, ColonyIndex = -1, CrowdCap = 16, SelfSlot = models[i].GridSlot,
                    VictimSlot = -1, FamiliarSlot = -1, StickMul = 1f, LowHpMul = 1f, FamiliarMul = 1f
                });
            }
            system.Schedule(grid, 0, n).Complete();

            var last = system.Output(3);
            Assert.AreEqual(models[2].GridSlot, last.NearestSlot);
            Assert.AreEqual(7f, last.NearestDistance, 1e-4f);
            Assert.AreEqual(models[1].GridSlot, system.Output(0).NearestSlot);

            system.RemoveSwapBack(0, 3);

            Assert.AreEqual(models[2].GridSlot, system.Output(0).NearestSlot);
            Assert.AreEqual(7f, system.Output(0).NearestDistance, 1e-4f);
            Assert.AreEqual(-1, system.Output(3).NearestSlot);
            Assert.AreEqual(-1f, system.Output(3).NearestDistance);
            Assert.AreEqual(0, system.Output(3).Crowd);

            system.RemoveSwapBack(1, 99);
            Assert.AreEqual(models[0].GridSlot, system.Output(1).NearestSlot);
        }
        finally
        {
            for (int i = 0; i < n; i++) models[i]?.Dispose();
            positions.Dispose();
            grid.Dispose();
            system.Dispose();
        }
    }

    [Test]
    public void OutOfRangeSwapBackIsIgnored()
    {
        var system = new CasteThresholdSystem(2);
        try
        {
            system.Reset(0, new float4(5f));
            system.RemoveSwapBack(0, 7);
            Assert.AreEqual(new float4(5f), system.Get(0));
        }
        finally
        {
            system.Dispose();
        }
    }

    [Test]
    public void SkillGraphStoreMovesEveryBufferOnSwapBack()
    {
        var atlas = SkillAtlas.BuildDefault();
        var topology = SkillGraphTopology.Create(atlas, default);
        var store = new SkillGraphStore(3, atlas.Count);
        try
        {
            for (int i = 0; i < 3; i++)
            {
                store.Reset(i, atlas.PositionOf(i), topology, atlas);
                store.SetLevel(i, 2, 10f * (i + 1), 5f * (i + 1));
                store.ClearDirty(i);
            }
            store.MarkDirty(2);
            var genes = GeneticParameters.Default;
            genes.PlastNeed = 1.75f;
            store.Retarget(2, in genes, topology, atlas);
            store.SetColony(2, 5);
            int lastRoot = store.Root(2);
            float lastTalent = store.Talent(2, 4);

            store.RemoveSwapBack(0, 2);

            Assert.AreEqual(30f, store.Level(0, 2));
            Assert.AreEqual(30f, store.Peak(0, 2));
            Assert.AreEqual(15f, store.LastUsed[0 * atlas.Count + 2]);
            Assert.AreEqual(lastTalent, store.Talent(0, 4));
            Assert.AreEqual(lastRoot, store.Root(0));
            Assert.AreEqual(5, store.ColonyOf(0));
            Assert.AreEqual(1.75f, store.PlasticityOf(0).w);
            Assert.IsTrue(store.IsDirty(0));
            Assert.AreEqual(20f, store.Level(1, 2));

            Assert.AreEqual(0f, store.Level(2, 2));
            Assert.AreEqual(0f, store.Peak(2, 2));
            Assert.AreEqual(0f, store.LastUsed[2 * atlas.Count + 2]);
            Assert.AreEqual(0f, store.Talent(2, 4));
            Assert.AreEqual(0, store.Root(2));
            Assert.AreEqual(0, store.ColonyOf(2));
            Assert.AreEqual(0f, store.PlasticityOf(2).w);
            Assert.IsFalse(store.IsDirty(2));

            store.RemoveSwapBack(1, 1);
            Assert.AreEqual(0f, store.Level(1, 2));
            store.RemoveSwapBack(0, 9);
            Assert.AreEqual(30f, store.Level(0, 2));
        }
        finally
        {
            store.Dispose();
            topology.Dispose();
            Object.DestroyImmediate(atlas);
        }
    }
}
