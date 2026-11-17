using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public class BrainAllocationTests
{
    private const int   LstmHidden = 32;
    private const float Dt         = 0.05f;
    private const int   Warmup     = 40;
    private const int   Measured   = 20;

    private SimulationRules _rules;
    private SharedHierarchicalBrain _brain;
    private readonly List<EntityBrainContext> _contexts = new();

    [SetUp]
    public void SetUp()
    {
        SimulationClock.Reset();
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(_rules);
        ContextSlabs.Reserve(8);
        _brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
    }

    [TearDown]
    public void TearDown()
    {
        _brain.CompletePending();
        for (int i = 0; i < _contexts.Count; i++) _contexts[i].Dispose();
        _contexts.Clear();
        _brain.Dispose();
        ContextSlabs.DisposeAll();
        UnityEngine.Object.DestroyImmediate(_rules);
        SimulationClock.Reset();
    }

    private EntityBrainContext NewContext()
    {
        var ctx = _brain.CreateContext();
        _contexts.Add(ctx);
        return ctx;
    }

    private static float[] Input(float seed)
    {
        var x = new float[InputVectorizer.VectorSize];
        for (int i = 0; i < x.Length; i++) x[i] = 0.1f * MathF.Sin(seed + i * 0.37f);
        return x;
    }

    private static void Warm(TestDelegate step)
    {
        for (int i = 0; i < Warmup; i++) step();
    }

    [Test]
    public void ValueCriticPredictAndTrainDoNotAllocate()
    {
        var critic = new ValueCritic(InputVectorizer.VectorSize + LstmHidden);
        var x = new float[InputVectorizer.VectorSize + LstmHidden];
        for (int i = 0; i < x.Length; i++) x[i] = 0.1f * MathF.Cos(i * 0.21f);
        float sink = 0f;

        TestDelegate step = () =>
        {
            sink += critic.Predict(x);
            sink += critic.TrainTD(x, 0.5f);
        };

        Warm(step);
        AllocationAssert.Zero(step, 200, "ValueCritic.Predict/TrainTD");
    }

    [Test]
    public void WarmDecisionWithRewardCompletionDoesNotAllocate()
    {
        var ctx = NewContext();
        var x   = Input(7f);
        float t = 0f;

        TestDelegate step = () =>
        {
            t += 50f;
            _brain.BeginDecisionBatch();
            int slot = _brain.EnqueueDecision(ctx, x, t, Dt);
            _brain.RunDecisions();
            _brain.CompletePending();
            if (_brain.TryGetDecision(slot, out BrainDecision decision))
                _brain.CompleteDecision(in decision, 0.5f, ctx, t + 1f, EntityCommandStatus.Completed);
            _brain.ApplyPendingGradients();
        };

        Warm(step);
        AllocationAssert.Zero(step, Measured, "EnqueueDecision/RunDecisions/CompleteDecision/ApplyPendingGradients");
    }

    [Test]
    public void WarmTerminalFlushDoesNotAllocate()
    {
        var ctx = NewContext();
        var x   = Input(9f);
        float t = 0f;

        TestDelegate step = () =>
        {
            t += 50f;
            _brain.BeginDecisionBatch();
            _brain.EnqueueDecision(ctx, x, t, Dt);
            _brain.RunDecisions();
            _brain.CompleteTerminal(ctx, -1f, t + 1f);
            _brain.BeginDecisionBatch();
            _brain.RunDecisions();
            _brain.ApplyPendingGradients();
        };

        Warm(step);
        AllocationAssert.Zero(step, Measured, "CompleteTerminal/FlushTerminal/ApplyPendingGradients");
    }

    [Test]
    public void VectorizeWithNewInputsDoesNotAllocate()
    {
        var vectorizer = new InputVectorizer(100f, 100f);
        var frame = new VectorizerFrame
        {
            Health = 60f, Energy = 40f, DayPhase = 0.3f,
            ObjectDir = new float2(0.6f, 0.8f), DangerDir = new float2(-1f, 0f), HeldObject = 1f, ObjectDist = 3f,
            SpeechBag = new float[SimulationRules.SpeechVocab],
            PlanRewardEma = new float[PlanCatalog.Count],
            MimickedAction = (int)EntityAction.Give,
        };
        var times = new double[ActionCatalog.Count];
        float sink = 0f;

        TestDelegate step = () => sink += vectorizer.Vectorize(in frame, times, in TerrainSample.Invalid)[InputVectorizer.StateOffset];

        Warm(step);
        AllocationAssert.Zero(step, 200, "InputVectorizer.Vectorize");
    }

    private sealed class ObjectWorld : IWorldCommandTarget
    {
        public readonly Dictionary<long, int> Objects = new();

        public bool TryGetObject(long worldPos, out ObjectInstInfo info)
        {
            if (Objects.TryGetValue(worldPos, out int id))
            {
                info = new ObjectInstInfo(Vector3.zero, id, 1);
                return true;
            }
            info = default;
            return false;
        }

        public bool RemoveObject(long worldPos) => Objects.Remove(worldPos);

        public bool TryAddObject(long worldPos, Vector3 realPos, int prefabID, int amount, ReadOnlySpan<Vector2Int> additionalWorldCells, int levels) => false;

        public Vector3 GetRealPosition(long worldPos) => Vector3.zero;
    }

    private static ObjectInfo MakeInfo(ushort id, bool pickable, bool food)
    {
        var info = ScriptableObject.CreateInstance<ObjectInfo>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ObjectInfo).GetField("id", flags).SetValue(info, id);
        typeof(ObjectInfo).GetField("isFood", flags).SetValue(info, food);
        if (pickable)
            typeof(ObjectInfo).GetField("inventoryItemInfo", flags).SetValue(info, ScriptableObject.CreateInstance<InventoryItemInfo>());
        return info;
    }

    [Test]
    public void ObjectScanFindsTheNearestPickableAndDoesNotAllocate()
    {
        var registry = ScriptableObject.CreateInstance<ObjectInfoRegistry>();
        var infos = new[] { MakeInfo(1, true, false), MakeInfo(2, false, false), MakeInfo(3, true, true) };
        try
        {
            registry.objectInfos.AddRange(infos);
            registry.Rebuild();

            var self = new float2(10.5f, 20.5f);
            Position2Int.Unpack(ChunkFoodIndex.CellOf(self.x, self.y), out int cx, out int cy);
            var world = new ObjectWorld();
            long far     = Position2Int.Pack(cx + 3, cy);
            long near    = Position2Int.Pack(cx, cy + 2);
            long nearest = Position2Int.Pack(cx + 1, cy);
            world.Objects[far]     = 1;
            world.Objects[near]    = 1;
            world.Objects[nearest] = 2;
            world.Objects[Position2Int.Pack(cx, cy - 1)] = 3;
            world.Objects[Position2Int.Pack(cx + 40, cy)] = 1;

            bool found = ObjectSensor.TryFindNearest(world, registry, in self, _rules.ObjectSenseRadius, out float2 pos, out float dist);
            Assert.IsTrue(found);
            Assert.AreEqual(ChunkFoodIndex.CellCenter(near).x, pos.x, 1e-4f);
            Assert.AreEqual(ChunkFoodIndex.CellCenter(near).y, pos.y, 1e-4f);
            Assert.AreEqual(math.distance(self, pos), dist, 1e-4f);

            Assert.IsFalse(ObjectSensor.TryFindNearest(world, registry, in self, 0, out _, out float none));
            Assert.AreEqual(-1f, none);

            float sink = 0f;
            int radius = _rules.ObjectSenseRadius;
            TestDelegate step = () =>
            {
                ObjectSensor.TryFindNearest(world, registry, in self, radius, out float2 p, out float d);
                sink += p.x + d;
            };

            Warm(step);
            AllocationAssert.Zero(step, 200, "ObjectSensor.TryFindNearest");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(registry);
            for (int i = 0; i < infos.Length; i++) UnityEngine.Object.DestroyImmediate(infos[i]);
        }
    }
}
