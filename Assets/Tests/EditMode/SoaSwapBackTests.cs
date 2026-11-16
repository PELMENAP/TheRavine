using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

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
                    Position = positions[i], NearestRadius = 20f, CrowdRadius = 20f, ColonyIndex = -1, CrowdCap = 16, SelfSlot = models[i].GridSlot
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
}
