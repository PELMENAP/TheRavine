using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class MimicLegJobTests
{
    private const int Samples = 12;
    private const int Ring = 5;
    private const int Resolution = 40;
    private const float HalfWidth = 0.1f;

    private static LegParams Leg(LegState state, float3 foot) => new LegParams
    {
        State = state,
        Foot = foot,
        Progression = 0f,
        GrowTarget = 1f,
        GrowCoef = 5f,
        LifeTimer = 10f,
        LegHeight = 2f,
        RotationSpeed = 20f,
        OscillationSpeed = 2f,
        OscillationProgress = 0f,
    };

    private static MimicWork Run(LegStore store, float3 position, float dt, int samples)
    {
        var work = new NativeArray<MimicWork>(1, Allocator.TempJob);
        try
        {
            work[0] = store.MakeWork(position, dt, HalfWidth, samples, Ring, Resolution);
            new MimicLegJob { Work = work }.Run(1);
            return work[0];
        }
        finally
        {
            work.Dispose();
        }
    }

    [Test]
    public void ActiveLegsProduceTubesAndDisabledLegsAreSkipped()
    {
        int perLeg = (Samples - 1) * Ring + 1;
        var store = new LegStore(3, perLeg * 3);
        try
        {
            store[0] = Leg(LegState.Growing, new float3(6f, 0f, 1f));
            store[2] = Leg(LegState.Supporting, new float3(-4f, 0f, 3f));

            var result = Run(store, new float3(10f, 5f, 10f), 0.1f, Samples);

            Assert.AreEqual(2, result.ActiveLegs);
            Assert.AreEqual(2 * perLeg, result.VertexCount);
            Assert.AreEqual(LegState.Disabled, store[1].State);

            var vertices = store.VertexView;
            var normals = store.NormalView;
            for (int i = 0; i < result.VertexCount; i++)
                Assert.AreEqual(1f, math.length((float3)normals[i]), 1e-3f);

            for (int leg = 0; leg < 2; leg++)
            {
                for (int j = 0; j < Ring; j++)
                    Assert.AreEqual(HalfWidth, math.length((float3)vertices[leg * perLeg + j]), 1e-3f);
            }
        }
        finally
        {
            store.Dispose();
        }
    }

    [Test]
    public void AnimationAdvancesWithoutMeshWhenSamplesAreZero()
    {
        var store = new LegStore(2, 8);
        try
        {
            store[0] = Leg(LegState.Growing, new float3(3f, 0f, 0f));

            var result = Run(store, float3.zero, 0.1f, 0);

            Assert.AreEqual(1, result.ActiveLegs);
            Assert.AreEqual(0, result.VertexCount);
            Assert.AreEqual(0.5f, store[0].Progression, 1e-4f);
            Assert.AreEqual(0.2f, store[0].OscillationProgress, 1e-4f);
        }
        finally
        {
            store.Dispose();
        }
    }

    [Test]
    public void ProgressionConvergesAndStaysClamped()
    {
        var store = new LegStore(1, (Samples - 1) * Ring + 1);
        try
        {
            store[0] = Leg(LegState.Supporting, new float3(3f, 0f, 0f));
            for (int i = 0; i < 100; i++) Run(store, float3.zero, 1f, Samples);

            Assert.AreEqual(1f, store[0].Progression, 1e-4f);
            Assert.Less(store[0].OscillationProgress, 360f);
        }
        finally
        {
            store.Dispose();
        }
    }
}
