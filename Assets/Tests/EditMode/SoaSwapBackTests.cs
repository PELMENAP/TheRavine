using NUnit.Framework;
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
            int lastRoot = store.Root(2);
            float lastTalent = store.Talent(2, 4);

            store.RemoveSwapBack(0, 2);

            Assert.AreEqual(30f, store.Level(0, 2));
            Assert.AreEqual(30f, store.Peak(0, 2));
            Assert.AreEqual(15f, store.LastUsed[0 * atlas.Count + 2]);
            Assert.AreEqual(lastTalent, store.Talent(0, 4));
            Assert.AreEqual(lastRoot, store.Root(0));
            Assert.IsTrue(store.IsDirty(0));
            Assert.AreEqual(20f, store.Level(1, 2));

            Assert.AreEqual(0f, store.Level(2, 2));
            Assert.AreEqual(0f, store.Peak(2, 2));
            Assert.AreEqual(0f, store.LastUsed[2 * atlas.Count + 2]);
            Assert.AreEqual(0f, store.Talent(2, 4));
            Assert.AreEqual(0, store.Root(2));
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
