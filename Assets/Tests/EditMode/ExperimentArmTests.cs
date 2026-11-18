using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class ExperimentArmTests
{
    private const int LstmHidden = 32;

    private SimulationRules _rules;

    [SetUp]
    public void SetUp()
    {
        SimulationClock.Reset();
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(_rules);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_rules);
        SimulationRules.Bind(null);
        SimulationClock.Reset();
    }

    private void Set(string field, object value)
    {
        typeof(SimulationRules).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_rules, value);
        SimulationRules.Bind(_rules);
    }

    [Test]
    public void DefaultRulesAssignControlOnly()
    {
        for (int i = 0; i < 200; i++) Assert.AreEqual(ColonyArm.Control, ColonyArms.Draw(_rules, false));
    }

    [Test]
    public void WildColonyIsAlwaysControl()
    {
        Set("armNullPolicyFraction", 1f);
        for (int i = 0; i < 50; i++) Assert.AreEqual(ColonyArm.Control, ColonyArms.Draw(_rules, true));
    }

    [Test]
    public void FractionsSelectTheArm()
    {
        Set("armSpeechMutedFraction", 1f);
        for (int i = 0; i < 50; i++) Assert.AreEqual(ColonyArm.SpeechMuted, ColonyArms.Draw(_rules, false));

        Set("armSpeechMutedFraction", 0f);
        Set("armSpeechShuffledFraction", 1f);
        for (int i = 0; i < 50; i++) Assert.AreEqual(ColonyArm.SpeechShuffled, ColonyArms.Draw(_rules, false));

        Set("armSpeechShuffledFraction", 0f);
        Set("armNullPolicyFraction", 1f);
        for (int i = 0; i < 50; i++) Assert.AreEqual(ColonyArm.NullPolicy, ColonyArms.Draw(_rules, false));
    }

    [Test]
    public void UnknownArmValueLoadsAsControl()
    {
        Assert.AreEqual(ColonyArm.NullPolicy, ColonyArms.FromByte((byte)ColonyArm.NullPolicy));
        Assert.AreEqual(ColonyArm.Control, ColonyArms.FromByte(200));
        Assert.AreEqual(ColonyArm.Control, ColonyArms.FromByte((byte)ColonyArm.Count));
        Assert.AreEqual("control", ColonyArms.Name((ColonyArm)99));
    }

    private sealed class Room
    {
        public readonly SpeechBus Bus;
        public readonly EntitySpatialGrid Grid = new();
        public readonly EntityModel[] Models;
        private readonly NativeArray<float3> _positions;

        public Room(ColonyArm arm)
        {
            const int n = 3;
            Bus = new SpeechBus(n, new List<EntityModel>());
            Models = new EntityModel[n];
            _positions = new NativeArray<float3>(n, Allocator.Persistent);
            float[] xs = { 0f, 3f, 4f };
            for (int i = 0; i < n; i++)
            {
                _positions[i] = new float3(xs[i], 0f, 0f);
                Models[i] = new EntityModel { ManagerIndex = i };
            }
            Grid.Rebuild(Models, n, 100f, _positions);
            Bus.Reset(0, 0, 1, false, arm);
            Bus.Reset(1, 0, 2, false);
            Bus.Reset(2, 1, 3, false);
        }

        public Utterance Newest(int listener)
        {
            var best = default(Utterance);
            for (int s = 0; s < SimulationRules.SpeechInbox; s++)
            {
                var u = Bus.InboxAt(listener, s);
                if (u.Len != 0 && u.Time >= best.Time) best = u;
            }
            return best;
        }

        public void Say(ushort tokens, byte context, float time)
        {
            Bus.Emit(new Utterance
            {
                Tokens = tokens, Len = SimulationRules.SpeechLen, Loudness = 255, Context = context,
                Speaker = 0, Pos = float2.zero, Time = time,
            });
            Bus.Flush(Grid, time);
        }

        public void Dispose()
        {
            for (int i = 0; i < Models.Length; i++) Models[i]?.Dispose();
            _positions.Dispose();
            Grid.Dispose();
            Bus.Dispose();
        }
    }

    [Test]
    public void ArmFollowsSwapBackInSpeechBus()
    {
        var room = new Room(ColonyArm.SpeechShuffled);
        try
        {
            Assert.AreEqual(ColonyArm.SpeechShuffled, room.Bus.ArmOf(0));
            room.Bus.RemoveSwapBack(0, 2);
            Assert.AreEqual(ColonyArm.Control, room.Bus.ArmOf(0));
            Assert.AreEqual(ColonyArm.Control, room.Bus.ArmOf(2));
        }
        finally { room.Dispose(); }
    }

    [Test]
    public void ControlDeliversTheSentToken()
    {
        var room = new Room(ColonyArm.Control);
        try
        {
            room.Say(Utterance.Repeat(5), 3, 1f);
            var heard = room.Newest(1);
            Assert.AreEqual(5, heard.Token(0));
            Assert.AreEqual(5, heard.Token(1));
            Assert.AreEqual(3, heard.Context);
        }
        finally { room.Dispose(); }
    }

    [Test]
    public void MutedSpeakerDeliversNothingAndStillEmits()
    {
        var room = new Room(ColonyArm.SpeechMuted);
        try
        {
            for (int i = 0; i < 10; i++) room.Say(Utterance.Repeat(5), 3, i + 1f);
            Assert.AreEqual(10, room.Bus.Emitted);
            Assert.AreEqual(0, room.Bus.Delivered);
            Assert.AreEqual(0, room.Newest(1).Len);
            Assert.AreEqual(0, room.Newest(2).Len);
        }
        finally { room.Dispose(); }
    }

    [Test]
    public void ShuffledKeepsTimingRadiusAndCountOfControl()
    {
        var control  = new Room(ColonyArm.Control);
        var shuffled = new Room(ColonyArm.SpeechShuffled);
        try
        {
            for (int i = 0; i < 25; i++)
            {
                ushort tokens = Utterance.Repeat(i % 2 == 0 ? 1 : 14);
                control.Say(tokens, (byte)(1 + i % 2), i + 1f);
                shuffled.Say(tokens, (byte)(1 + i % 2), i + 1f);
            }
            Assert.AreEqual(control.Bus.Emitted, shuffled.Bus.Emitted);
            Assert.AreEqual(control.Bus.Delivered, shuffled.Bus.Delivered);

            var a = control.Newest(1);
            var b = shuffled.Newest(1);
            Assert.AreEqual(a.Time, b.Time);
            Assert.AreEqual(a.Loudness, b.Loudness);
            Assert.AreEqual(a.Len, b.Len);
            Assert.AreEqual(a.Pos, b.Pos);
        }
        finally
        {
            control.Dispose();
            shuffled.Dispose();
        }
    }

    [Test]
    public void ShuffledTokensComeFromTheRecentDistribution()
    {
        var room = new Room(ColonyArm.SpeechShuffled);
        try
        {
            for (int i = 0; i < 12; i++)
            {
                room.Say(Utterance.Repeat(5), 3, i + 1f);
                if (i == 0) continue;
                var heard = room.Newest(1);
                Assert.AreEqual(5, heard.Token(0));
                Assert.AreEqual(5, heard.Token(1));
                Assert.AreEqual(3, heard.Context);
            }
        }
        finally { room.Dispose(); }
    }

    [Test]
    public void ShuffledChannelBreaksTokenContextPairing()
    {
        var room = new Room(ColonyArm.SpeechShuffled);
        try
        {
            int mismatched = 0, samples = 0;
            for (int i = 0; i < 400; i++)
            {
                bool even = i % 2 == 0;
                room.Say(Utterance.Repeat(even ? 1 : 14), (byte)(even ? 1 : 2), i + 1f);
                if (i < 20) continue;
                var heard = room.Newest(1);
                bool tokenSaysEven = heard.Token(0) == 1;
                bool contextSaysEven = heard.Context == 1;
                if (tokenSaysEven != contextSaysEven) mismatched++;
                samples++;
            }
            Assert.Greater(mismatched, samples / 5);
        }
        finally { room.Dispose(); }
    }

    [Test]
    public void ShuffledUtteranceReachesListenerOfAnotherColony()
    {
        var room = new Room(ColonyArm.SpeechShuffled);
        try
        {
            for (int i = 0; i < 5; i++) room.Say(Utterance.Repeat(9), 4, i + 1f);
            var foreign = room.Newest(2);
            Assert.AreEqual(SimulationRules.SpeechLen, foreign.Len);
            Assert.IsFalse(foreign.IsKin);
            Assert.IsTrue(room.Newest(1).IsKin);
        }
        finally { room.Dispose(); }
    }

    [Test]
    public void DeliveryDoesNotAllocateInAnyArm()
    {
        foreach (var arm in new[] { ColonyArm.Control, ColonyArm.SpeechMuted, ColonyArm.SpeechShuffled })
        {
            var room = new Room(arm);
            try
            {
                float t = 0f;
                var utterance = new Utterance
                {
                    Tokens = Utterance.Repeat(3), Len = SimulationRules.SpeechLen, Loudness = 255, Context = 5,
                    Speaker = 0, Pos = float2.zero,
                };
                AllocationAssert.Zero(() =>
                {
                    t += 1f;
                    utterance.Time = t;
                    room.Bus.Emit(in utterance);
                    room.Bus.Flush(room.Grid, t);
                }, 50, $"SpeechBus.Emit/Flush {arm}");
            }
            finally { room.Dispose(); }
        }
    }

    private static Utterance Voiced(ushort tokens, byte context)
        => new Utterance { Tokens = tokens, Len = SimulationRules.SpeechLen, Context = context };

    [Test]
    public void SpeakerConsistencyIsZeroForIndependentTokensAndHighWhenDeterministic()
    {
        var independent = new ColonySpeechTables();
        var rng = new System.Random(3);
        for (int i = 0; i < 20000; i++)
            independent.RecordEmission(Voiced(Utterance.Repeat(rng.Next(SimulationRules.SpeechVocab)), (byte)rng.Next(ColonySpeechTables.Contexts)));
        Assert.Less(independent.MutualInformationBits(), 0.05f);

        var deterministic = new ColonySpeechTables();
        for (int i = 0; i < 20000; i++)
        {
            int c = rng.Next(SimulationRules.SpeechVocab);
            deterministic.RecordEmission(Voiced(Utterance.Repeat(c), (byte)c));
        }
        Assert.AreEqual(4f, deterministic.MutualInformationBits(), 0.05f);

        deterministic.Clear();
        Assert.AreEqual(0f, deterministic.MutualInformationBits());
    }

    [Test]
    public void ListenerShiftIsTotalVariationBetweenFreshAndSilent()
    {
        var t = new ColonySpeechTables();
        Assert.AreEqual(0f, t.ListenerShift());

        for (int i = 0; i < 10; i++) { t.RecordListen(false, (int)EntityAction.Idle); t.RecordListen(true, (int)EntityAction.Idle); }
        Assert.AreEqual(0f, t.ListenerShift(), 1e-6f);

        t.Clear();
        for (int i = 0; i < 10; i++) { t.RecordListen(false, (int)EntityAction.Idle); t.RecordListen(true, (int)EntityAction.Signal); }
        Assert.AreEqual(1f, t.ListenerShift(), 1e-6f);

        t.Clear();
        for (int i = 0; i < 10; i++) t.RecordListen(false, (int)EntityAction.Idle);
        Assert.AreEqual(0f, t.ListenerShift());
    }

    [Test]
    public void GradientConflictCosinesAndNorms()
    {
        var c = new GradientConflict();
        c.Begin();
        c.Sq[0] = 4d; c.Sq[1] = 9d; c.Sq[2] = 1d;
        c.Count[0] = 2; c.Count[1] = 3; c.Count[2] = 1;
        c.Dot[0 * GradientConflict.Groups + 1] = 6d;
        c.Dot[0 * GradientConflict.Groups + 2] = -2d;
        c.Finish();

        Assert.IsTrue(c.Fresh);
        Assert.AreEqual(1f, c.Cos[GradientConflict.PairIndex(0, 1)], 1e-5f);
        Assert.AreEqual(-1f, c.Cos[GradientConflict.PairIndex(0, 2)], 1e-5f);
        Assert.IsNaN(c.Cos[GradientConflict.PairIndex(0, 3)]);
        Assert.AreEqual(1f, c.Norm[0], 1e-5f);
        Assert.AreEqual(1f, c.Norm[1], 1e-5f);
        Assert.IsNaN(c.Norm[3]);

        var seen = new HashSet<int>();
        for (int a = 0; a < GradientConflict.Groups; a++)
            for (int b = a + 1; b < GradientConflict.Groups; b++) seen.Add(GradientConflict.PairIndex(a, b));
        Assert.AreEqual(GradientConflict.Pairs, seen.Count);
        foreach (int index in seen) Assert.That(index, Is.InRange(0, GradientConflict.Pairs - 1));
    }

    private static float[] Input(float seed)
    {
        var x = new float[InputVectorizer.VectorSize];
        for (int i = 0; i < x.Length; i++) x[i] = 0.1f * Mathf.Sin(seed + i * 0.37f);
        return x;
    }

    private const PlanHint Hints = PlanHint.HasNest | PlanHint.HasHome | PlanHint.FoodVisible | PlanHint.Hungry
                                 | PlanHint.CanReproduce | PlanHint.EntityNear | PlanHint.AtNest;

    [Test]
    public void NullPolicyDrawsUniformlyFromAdmissibleSetAndNeverTrains()
    {
        ContextSlabs.Reserve(8);
        var brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
        brain.SetNullPolicy(true);
        var ctx = brain.CreateContext();
        try
        {
            var planCounts = new Dictionary<PlanKind, int>();
            var seenActions = new HashSet<int>();
            var x = Input(1f);
            float t = 0f;
            int decided = 0;
            for (int i = 0; i < 600; i++)
            {
                t += 1000f;
                ctx.PlanHints = Hints;
                ctx.CasteMask = -1;
                brain.BeginDecisionBatch();
                int slot = brain.EnqueueDecision(ctx, x, t, 0.05f);
                brain.RunDecisions();
                brain.CompletePending();
                if (!brain.TryGetDecision(slot, out var decision)) continue;

                decided++;
                Assert.IsTrue(decision.HasPlan);
                Assert.IsTrue(PlanCatalog.IsFeasible(decision.Plan, Hints, -1));
                Assert.AreNotEqual(0, PlanCatalog.EntryMask(decision.Plan, Hints) & (1 << decision.Action));
                planCounts.TryGetValue(decision.Plan, out int seen);
                planCounts[decision.Plan] = seen + 1;
                seenActions.Add(decision.Action);

                brain.CompleteDecision(in decision, (i & 1) == 0 ? 2f : -2f, ctx, t + 1f, EntityCommandStatus.Completed);
                brain.ApplyPendingGradients();
            }

            int most = 0, least = int.MaxValue;
            foreach (var pair in planCounts)
            {
                most  = Mathf.Max(most, pair.Value);
                least = Mathf.Min(least, pair.Value);
            }

            Assert.Greater(decided, 300);
            Assert.Greater(planCounts.Count, 1);
            Assert.Less(most, 3 * least);
            Assert.Greater(seenActions.Count, 1);

            brain.CompleteTerminal(ctx, -1f, t + 2f);
            brain.BeginDecisionBatch();
            brain.RunDecisions();
            brain.ApplyPendingGradients();

            Assert.AreEqual(0, ctx.CoordMLP.TrainingSteps);
            for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++) Assert.AreEqual(0, ctx.ExecMLPs[g].TrainingSteps);
        }
        finally
        {
            brain.CompletePending();
            ctx.Dispose();
            brain.Dispose();
            ContextSlabs.DisposeAll();
        }
    }

    [Test]
    public void GradientConflictIsMeasuredOnlyWhenTheFlagIsOn()
    {
        Set("gradientConflictInterval", 1);
        ContextSlabs.Reserve(8);
        var brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
        var a = brain.CreateContext();
        var b = brain.CreateContext();
        try
        {
            a.SetGroup((int)Caste.Worker);
            b.SetGroup((int)Caste.Soldier);
            Assert.IsNull(Run(brain, a, b, 40));
            Assert.IsNull(brain.Conflict);

            Set("gradientConflictEnabled", true);
            var measured = Run(brain, a, b, 80);
            Assert.IsNotNull(brain.Conflict);
            Assert.IsNotNull(measured);
        }
        finally
        {
            brain.CompletePending();
            a.Dispose();
            b.Dispose();
            brain.Dispose();
            ContextSlabs.DisposeAll();
        }
    }

    private static GradientConflict Run(SharedHierarchicalBrain brain, EntityBrainContext a, EntityBrainContext b, int steps)
    {
        GradientConflict fresh = null;
        var xa = Input(2f);
        var xb = Input(5f);
        float t = SimulationClock.Time;
        for (int i = 0; i < steps; i++)
        {
            t += 1000f;
            brain.BeginDecisionBatch();
            int sa = brain.EnqueueDecision(a, xa, t, 0.05f);
            int sb = brain.EnqueueDecision(b, xb, t, 0.05f);
            brain.RunDecisions();
            brain.CompletePending();
            if (brain.TryGetDecision(sa, out var da)) brain.CompleteDecision(in da, (i & 1) == 0 ? 2f : -2f, a, t + 1f, EntityCommandStatus.Completed);
            if (brain.TryGetDecision(sb, out var db)) brain.CompleteDecision(in db, (i & 2) == 0 ? 2f : -2f, b, t + 1f, EntityCommandStatus.Completed);
            brain.ApplyPendingGradients();
            if (brain.Conflict != null && brain.Conflict.Fresh) fresh = brain.Conflict;
        }
        return fresh;
    }
}
