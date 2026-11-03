using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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
}
