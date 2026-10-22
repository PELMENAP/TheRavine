using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BrainLearningTests
{
    private const int   LstmHidden = 32;
    private const float Dt         = 0.05f;

    private SimulationRules _rules;
    private SharedHierarchicalBrain _brain;
    private readonly List<EntityBrainContext> _contexts = new();

    [SetUp]
    public void SetUp()
    {
        SimulationClock.Reset();
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        var so = new SerializedObject(_rules);
        so.FindProperty("coordCuriosityWeight").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
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

    private void Step(EntityBrainContext ctx, float[] input, float time, bool allowDecision = true)
    {
        _brain.BeginDecisionBatch();
        _brain.EnqueueDecision(ctx, input, time, Dt, allowDecision);
        _brain.RunDecisions();
        _brain.CompletePending();
    }

    private void Train()
    {
        _brain.BeginDecisionBatch();
        _brain.RunDecisions();
        _brain.ApplyPendingGradients();
    }

    private float[] CoordinatorOutputBiases()
    {
        byte[] data = _brain.ToSnapshot().Serialize();
        using var br = new BinaryReader(new MemoryStream(data));
        br.ReadUInt32();
        br.ReadInt32();
        br.ReadBytes(br.ReadInt32());

        using var cr = new BinaryReader(new MemoryStream(br.ReadBytes(br.ReadInt32())));
        int count = cr.ReadInt32();
        var sizes = new int[count];
        for (int i = 0; i < count; i++) sizes[i] = cr.ReadInt32();
        int layers = count - 1;

        for (int part = 0; part < 2; part++)
            for (int l = 0; l < layers; l++)
                for (int n = 0; n < sizes[l + 1]; n++)
                    cr.ReadBytes(cr.ReadInt32() * sizeof(float));

        for (int l = 0; l < layers - 1; l++)
            cr.ReadBytes(cr.ReadInt32() * sizeof(float));

        var biases = new float[cr.ReadInt32()];
        for (int i = 0; i < biases.Length; i++) biases[i] = cr.ReadSingle();
        return biases;
    }

    [TestCase(-5f)]
    [TestCase(5f)]
    public void TerminalSignalMovesChosenLogitWithItsSign(float penalty)
    {
        var ctx   = NewContext();
        var input = Input(1f);
        Step(ctx, input, 0f);

        Assert.AreNotEqual(0, ctx.CoordDecisionId, "координатор не принял решение");
        var item = ctx.CoordMLP.Decisions.Find(ctx.CoordDecisionId);
        Assert.NotNull(item);
        int pred = item.Predicted;

        float[] before = CoordinatorOutputBiases();
        int stepsBefore = ctx.CoordMLP.TrainingSteps;

        _brain.CompleteTerminal(ctx, penalty, 0f);
        Assert.Greater(ctx.CoordMLP.TrainingSteps, stepsBefore, "терминальный flush не поставил обучение");
        Assert.AreEqual(0, ctx.CoordMLP.Decisions.Count);

        Train();
        float[] after = CoordinatorOutputBiases();

        float delta = after[pred] - before[pred];
        Assert.AreNotEqual(0f, delta, "градиент не применён");
        Assert.AreEqual(Math.Sign(penalty), Math.Sign(delta),
            $"penalty={penalty}: logit выбранного плана сдвинулся на {delta}");
    }

    [Test]
    public void CriticTdTargetSignFollowsTarget()
    {
        var critic = new ValueCritic(InputVectorizer.VectorSize + LstmHidden);
        var x = new float[InputVectorizer.VectorSize + LstmHidden];
        for (int i = 0; i < x.Length; i++) x[i] = 0.1f * MathF.Cos(i * 0.21f);

        float v0 = critic.Predict(x);
        Assert.Less(critic.TrainTD(x, v0 - 1f), 0f);
        for (int i = 0; i < 200; i++) critic.TrainTD(x, -1f);
        float v1 = critic.Predict(x);
        Assert.Less(v1, v0);
        Assert.Greater(critic.TrainTD(x, v1 + 1f), 0f);
    }

    [Test]
    public void ReservoirStateIsPerAgent()
    {
        var a = NewContext();
        var b = NewContext();

        _brain.BeginDecisionBatch();
        _brain.EnqueueDecision(a, Input(1f), 0f, Dt);
        _brain.EnqueueDecision(b, Input(2f), 0f, Dt);
        _brain.RunDecisions();
        _brain.CompletePending();

        float[] ha = a.Reservoir.H.ToArray();
        float[] hb = b.Reservoir.H.ToArray();
        float[] cb = b.Reservoir.C.ToArray();
        CollectionAssert.AreNotEqual(new float[LstmHidden], ha);
        CollectionAssert.AreNotEqual(ha, hb);

        Step(a, Input(3f), 1f, false);
        CollectionAssert.AreEqual(hb, b.Reservoir.H.ToArray());
        CollectionAssert.AreEqual(cb, b.Reservoir.C.ToArray());
        CollectionAssert.AreNotEqual(ha, a.Reservoir.H.ToArray());

        a.ResetMemory();
        CollectionAssert.AreEqual(new float[LstmHidden], a.Reservoir.H.ToArray());
        CollectionAssert.AreEqual(hb, b.Reservoir.H.ToArray());

        a.Dispose();
        _contexts.Remove(a);
        var c = NewContext();
        CollectionAssert.AreEqual(new float[LstmHidden], c.Reservoir.H.ToArray());
        CollectionAssert.AreEqual(hb, b.Reservoir.H.ToArray());
    }

    [Test]
    public void SameInputGivesSameStateAcrossAgents()
    {
        var a = NewContext();
        var b = NewContext();
        var x = Input(4f);

        _brain.BeginDecisionBatch();
        _brain.EnqueueDecision(a, x, 0f, Dt);
        _brain.EnqueueDecision(b, x, 0f, Dt);
        _brain.RunDecisions();
        _brain.CompletePending();

        CollectionAssert.AreEqual(a.Reservoir.H.ToArray(), b.Reservoir.H.ToArray());
    }

    [Test]
    public void WarmDecisionBatchDoesNotAllocate()
    {
        var a  = NewContext();
        var b  = NewContext();
        var xa = Input(5f);
        var xb = Input(6f);
        float t = 0f;

        TestDelegate batch = () =>
        {
            t += 50f;
            _brain.BeginDecisionBatch();
            _brain.EnqueueDecision(a, xa, t, Dt);
            _brain.EnqueueDecision(b, xb, t, Dt);
            _brain.RunDecisions();
            _brain.ApplyPendingGradients();
        };

        for (int i = 0; i < 40; i++) batch();
        AllocationAssert.Zero(batch, 20, "EnqueueDecision/RunDecisions/ApplyPendingGradients");
    }
}
