using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public class VerbSetTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp()
    {
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(_rules);
    }

    [TearDown]
    public void TearDown()
    {
        SimulationRules.Bind(null);
        Object.DestroyImmediate(_rules);
    }

    private static int Mask(SharedHierarchicalBrain.Goal goal)
    {
        int mask = 0;
        var subset = SharedHierarchicalBrain.ActionSubsets[(int)goal];
        for (int i = 0; i < subset.Length; i++) mask |= 1 << subset[i];
        return mask;
    }

    [Test]
    public void SubsetsMatchTheContract()
    {
        CollectionAssert.AreEqual(
            new[] { (int)EntityAction.Idle, (int)EntityAction.MoveTo, (int)EntityAction.Eat, (int)EntityAction.Rest },
            SharedHierarchicalBrain.ActionSubsets[(int)SharedHierarchicalBrain.Goal.Survive]);
        CollectionAssert.AreEqual(
            new[] { (int)EntityAction.Idle, (int)EntityAction.MoveTo, (int)EntityAction.Attack, (int)EntityAction.Signal },
            SharedHierarchicalBrain.ActionSubsets[(int)SharedHierarchicalBrain.Goal.Hunt]);
        CollectionAssert.AreEqual(
            new[] { (int)EntityAction.MoveTo, (int)EntityAction.Eat, (int)EntityAction.Signal, (int)EntityAction.Take, (int)EntityAction.Put },
            SharedHierarchicalBrain.ActionSubsets[(int)SharedHierarchicalBrain.Goal.Forage]);
        CollectionAssert.AreEqual(
            new[] { (int)EntityAction.Idle, (int)EntityAction.MoveTo, (int)EntityAction.Reproduce, (int)EntityAction.Signal, (int)EntityAction.Give },
            SharedHierarchicalBrain.ActionSubsets[(int)SharedHierarchicalBrain.Goal.Social]);
        Assert.IsTrue(ActionCatalog.Get(EntityAction.MoveNest).Has(ActionFlags.PlanOnly));
    }

    [Test]
    public void EnumOrderIsFixed()
    {
        CollectionAssert.AreEqual(
            new[] { "Idle", "MoveTo", "Attack", "Eat", "Reproduce", "Signal", "Rest", "Give", "Take", "Put", "MoveNest" },
            System.Enum.GetNames(typeof(EntityAction)));
        CollectionAssert.AreEqual(
            new[] { "Food", "NestFood", "OwnPoi", "ColonyPoi", "Frontier", "Migration", "Flock", "Levy",
                    "Nest", "Escape", "Kin", "Danger", "Object", "Task", "Count", "None" },
            System.Enum.GetNames(typeof(DirectionKind)));
        Assert.LessOrEqual((int)DirectionKind.Count, 16);
    }

    [Test]
    public void EntryMasksStayInsideTheGoalSubset()
    {
        var rng = new Unity.Mathematics.Random(12345u);
        for (int p = 0; p < PlanCatalog.Count; p++)
        {
            var plan = (PlanKind)p;
            int allowed = Mask(PlanCatalog.GoalOf(plan)) | (1 << (int)EntityAction.MoveNest);
            for (int i = 0; i < 4096; i++)
            {
                var hints = (PlanHint)(rng.NextUInt() & 0x3FFFFFFFu);
                int mask = PlanCatalog.EntryMask(plan, hints);
                Assert.AreEqual(0, mask & ~allowed, $"{plan}: {hints}");
                if ((mask & (1 << (int)EntityAction.MoveTo)) != 0)
                    Assert.AreNotEqual(0, PlanCatalog.DirectionMask(plan, hints), $"{plan}: {hints}");
            }
        }
    }

    [Test]
    public void PlanStepsCarryTheMatchingDirection()
    {
        var p = new PlanProgress { Kind = PlanKind.Graze, MaxFailures = 3 };
        PlanCatalog.Init(ref p);
        int next = PlanCatalog.Next(ref p, PlanHint.FoodVisible, EntityCommandStatus.Completed, (int)EntityAction.Idle);
        Assert.AreEqual((int)EntityAction.MoveTo, next);
        Assert.AreEqual((byte)DirectionKind.Food, p.Direction);

        p = new PlanProgress { Kind = PlanKind.Flee, MaxFailures = 3 };
        PlanCatalog.Init(ref p);
        next = PlanCatalog.Next(ref p, PlanHint.Threat, EntityCommandStatus.Completed, (int)EntityAction.MoveTo);
        Assert.AreEqual((int)EntityAction.MoveTo, next);
        Assert.AreEqual((byte)DirectionKind.Escape, p.Direction);

        p = new PlanProgress { Kind = PlanKind.Harvest, MaxFailures = 3 };
        PlanCatalog.Init(ref p);
        next = PlanCatalog.Next(ref p, PlanHint.Carrying | PlanHint.CarryFull | PlanHint.HasNest, EntityCommandStatus.Completed, (int)EntityAction.Take);
        Assert.AreEqual((int)EntityAction.MoveTo, next);
        Assert.AreEqual((byte)DirectionKind.Nest, p.Direction);
        next = PlanCatalog.Next(ref p, PlanHint.Carrying | PlanHint.AtNest | PlanHint.HasNest, EntityCommandStatus.Completed, (int)EntityAction.MoveTo);
        Assert.AreEqual((int)EntityAction.Put, next);

        p = new PlanProgress { Kind = PlanKind.Migrate, MaxFailures = 3 };
        PlanCatalog.Init(ref p);
        next = PlanCatalog.Next(ref p, PlanHint.MigrationUrge, EntityCommandStatus.Completed, (int)EntityAction.MoveTo);
        Assert.AreEqual((int)EntityAction.MoveTo, next);
        Assert.AreEqual((byte)DirectionKind.Migration, p.Direction);
    }

    [Test]
    public void ExecAuxBlocksDoNotOverlap()
    {
        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            int motor     = g == (int)SharedHierarchicalBrain.Goal.Hunt ? SharedHierarchicalBrain.TempoAux + 1
                          : g == (int)SharedHierarchicalBrain.Goal.Social ? SharedHierarchicalBrain.SpeechAux + SharedHierarchicalBrain.SpeechOutputs
                          : 0;
            int dirStart  = SharedHierarchicalBrain.DirectionAux(g);
            int dirEnd    = dirStart + SharedHierarchicalBrain.DirectionOutputs;
            int offsetAt  = SharedHierarchicalBrain.DirOffsetAux(g);
            int decodeAt  = SharedHierarchicalBrain.ExecAux(g) - SharedHierarchicalBrain.DecodeOutputs;

            Assert.AreEqual(motor, dirStart, $"goal {g}");
            Assert.AreEqual(dirEnd, offsetAt, $"goal {g}");
            Assert.AreEqual(offsetAt + 1, decodeAt, $"goal {g}");
            Assert.LessOrEqual(SharedHierarchicalBrain.ExecAux(g), KernelLayout.MaxAux, $"goal {g}");
            Assert.AreEqual(SharedHierarchicalBrain.ActionSubsets[g].Length + 1 + SharedHierarchicalBrain.ExecAux(g),
                SharedHierarchicalBrain.BuildExecSizes(InputVectorizer.VectorSize + 32, g)[4], $"goal {g}");
        }
    }

    [Test]
    public void InputOffsetsAreContiguous()
    {
        Assert.AreEqual(InputVectorizer.TraceOffset + ActionCatalog.Count, InputVectorizer.DirectionOffset);
        Assert.AreEqual(InputVectorizer.DirectionOffset + InputVectorizer.DirectionSlots, InputVectorizer.StateOffset);
        Assert.AreEqual(InputVectorizer.StateOffset + InputVectorizer.StateSlots, InputVectorizer.TailOffset);
        Assert.AreEqual(InputVectorizer.TailOffset + InputVectorizer.TailSlots, InputVectorizer.PlanEmaOffset);
        Assert.AreEqual(InputVectorizer.PlanEmaOffset + PlanCatalog.Count, InputVectorizer.SeasonOffset);
        Assert.AreEqual(InputVectorizer.SeasonOffset + 2, InputVectorizer.VectorSize);
    }

    [Test]
    public void VectorizerWritesTheNewSlots()
    {
        var vectorizer = new InputVectorizer(100f, 100f);
        var frame = new VectorizerFrame
        {
            Health = 50f, Energy = 50f,
            ObjectDir = new float2(0.25f, 0.5f),
            DangerDir = new float2(0.75f, -0.5f),
            TaskDir = new float2(-0.25f, -0.75f),
            HeldObject = 1f,
            ObjectDist = 0f,
            TaskProximity = 0.125f,
            TaskReward = 0.375f,
            TaskKind = -1f,
            MimickedAction = ActionCatalog.Count - 1,
            SpeechBag = new float[SimulationRules.SpeechVocab],
            PlanRewardEma = new float[PlanCatalog.Count],
        };
        var times = new double[ActionCatalog.Count];

        var v = vectorizer.Vectorize(in frame, times, in TerrainSample.Invalid);
        int d = InputVectorizer.DirectionOffset;
        Assert.AreEqual(0.25f, v[d + 10]); Assert.AreEqual(0.5f, v[d + 11]);
        Assert.AreEqual(0.75f, v[d + 12]); Assert.AreEqual(-0.5f, v[d + 13]);
        Assert.AreEqual(-0.25f, v[d + 14]); Assert.AreEqual(-0.75f, v[d + 15]);
        Assert.AreEqual(1f, v[InputVectorizer.StateOffset + InputVectorizer.StateSlots - 1]);

        int tail = InputVectorizer.PlanEmaOffset - 4;
        Assert.AreEqual(1f, v[tail]);
        Assert.AreEqual(0.125f, v[tail + 1]);
        Assert.AreEqual(0.375f, v[tail + 2]);
        Assert.AreEqual(-1f, v[tail + 3]);

        frame.ObjectDist = -1f;
        frame.HeldObject = 0f;
        v = vectorizer.Vectorize(in frame, times, in TerrainSample.Invalid);
        Assert.AreEqual(0f, v[tail]);
        Assert.AreEqual(0f, v[InputVectorizer.StateOffset + InputVectorizer.StateSlots - 1]);
        for (int i = 0; i < v.Length; i++) Assert.IsTrue(float.IsFinite(v[i]), $"slot {i}");
    }

    private static InstinctOutput Reflex(in InstinctInput input)
    {
        var system = new InstinctSystem(1);
        try
        {
            system.Reset(0, new InstinctGenes
            {
                HungerOn = 0.5f, HungerOff = 0.8f, SatedFill = 0.9f, PanicHp = 0.2f, PanicDanger = 0.5f,
                Tether = 1f, Nursery = 1f, Home = 1f, PanicHome = 0f, CarryFull = 1f, Reinterrupt = 1f,
            });
            system.Write(0, in input);
            system.Schedule(0, 1).Complete();
            return system.Output(0);
        }
        finally
        {
            system.Dispose();
        }
    }

    private static InstinctInput Calm() => new InstinctInput
    {
        HpFraction = 1f, EnergyFraction = 1f, Age = 1000f, ParentDistance = -1f, NestDistance = -1f,
        FoodDistance = -1f, RunningAction = InstinctBits.NoReflex, RunningDirection = (byte)DirectionKind.None,
        PlanKind = (byte)PlanKind.Count, Dt = 1f,
    };

    [Test]
    public void ReflexCausesMapToVerbAndDirection()
    {
        var x = Calm();
        x.HpFraction = 0.05f;
        x.HasThreat = 1;
        var o = Reflex(in x);
        Assert.AreEqual((byte)EntityAction.MoveTo, o.Reflex);
        Assert.AreEqual((byte)DirectionKind.Escape, o.Direction);

        x = Calm();
        x.Carrying = 1f;
        o = Reflex(in x);
        Assert.AreEqual((byte)EntityAction.MoveTo, o.Reflex);
        Assert.AreEqual((byte)DirectionKind.Nest, o.Direction);

        x = Calm();
        x.Carrying = 0.1f;
        x.AtNest = 1;
        o = Reflex(in x);
        Assert.AreEqual((byte)EntityAction.Put, o.Reflex);
        Assert.AreEqual((byte)DirectionKind.None, o.Direction);

        x = Calm();
        x.AtNest = 1;
        x.HungryKin = 1;
        x.Stomach = 1f;
        o = Reflex(in x);
        Assert.AreEqual((byte)EntityAction.Give, o.Reflex);

        x = Calm();
        x.Recruited = 1;
        o = Reflex(in x);
        Assert.AreEqual((byte)EntityAction.MoveTo, o.Reflex);
        Assert.AreEqual((byte)DirectionKind.OwnPoi, o.Direction);

        x = Calm();
        x.EnergyFraction = 0f;
        x.FoodDistance = 50f;
        o = Reflex(in x);
        Assert.AreEqual((byte)EntityAction.MoveTo, o.Reflex);
        Assert.AreEqual((byte)DirectionKind.Food, o.Direction);

        x = Calm();
        x.EnergyFraction = 0f;
        x.FoodDistance = 0.5f;
        o = Reflex(in x);
        Assert.AreEqual((byte)EntityAction.Eat, o.Reflex);
    }

    [Test]
    public void ReflexPriorityFollowsVerbAndDirection()
    {
        var x = Calm();
        x.Carrying = 1f;
        x.Busy = 1;
        x.RunningInstinct = 1;
        x.RunningAction = (byte)EntityAction.MoveTo;
        x.RunningDirection = (byte)DirectionKind.Escape;
        Assert.AreEqual(InstinctBits.NoReflex, Reflex(in x).Reflex);

        x.RunningDirection = (byte)DirectionKind.Food;
        Assert.AreEqual((byte)EntityAction.MoveTo, Reflex(in x).Reflex);

        x.RunningInstinct = 0;
        x.RunningDirection = (byte)DirectionKind.Nest;
        Assert.AreEqual(InstinctBits.NoReflex, Reflex(in x).Reflex);

        x.RunningDirection = (byte)DirectionKind.Food;
        Assert.AreEqual((byte)EntityAction.MoveTo, Reflex(in x).Reflex);
    }
}
