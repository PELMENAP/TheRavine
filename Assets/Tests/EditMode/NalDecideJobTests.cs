using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using TheRavine.Generator;

public class NalDecideJobTests
{
    private const byte Nal = (byte)BehaviourType.NAL;
    private const byte Grow = (byte)BehaviourType.GROW;

    private NativeArray<int2> _cell;
    private NativeArray<int> _dense;
    private NativeArray<double> _due;
    private NativeArray<byte> _tier;
    private NativeArray<NalSpeciesParams> _species;
    private NativeArray<NalDecision> _output;
    private NativeArray<int> _counter;

    [TearDown]
    public void TearDown()
    {
        if (_cell.IsCreated) _cell.Dispose();
        if (_dense.IsCreated) _dense.Dispose();
        if (_due.IsCreated) _due.Dispose();
        if (_tier.IsCreated) _tier.Dispose();
        if (_species.IsCreated) _species.Dispose();
        if (_output.IsCreated) _output.Dispose();
        if (_counter.IsCreated) _counter.Dispose();
    }

    private static NalSpeciesParams Species(byte kind, byte chance, byte attempt, byte distance, byte delay, int evolution) =>
        new NalSpeciesParams
        {
            Kind = kind,
            Chance = chance,
            Attempt = attempt,
            Distance = distance,
            Delay = delay,
            HasDelay = 1,
            EvolutionDense = evolution,
            DeathMainDense = -1
        };

    private void Setup(int outputCapacity, double[] dues, params NalSpeciesParams[] species)
    {
        _cell = new NativeArray<int2>(dues.Length, Allocator.Persistent);
        _dense = new NativeArray<int>(dues.Length, Allocator.Persistent);
        _due = new NativeArray<double>(dues.Length, Allocator.Persistent);
        _tier = new NativeArray<byte>(dues.Length, Allocator.Persistent);
        for (int i = 0; i < dues.Length; i++)
        {
            _cell[i] = new int2(10 * i, -10 * i);
            _due[i] = dues[i];
        }

        _species = new NativeArray<NalSpeciesParams>(species, Allocator.Persistent);
        _output = new NativeArray<NalDecision>(outputCapacity, Allocator.Persistent);
        _counter = new NativeArray<int>(2, Allocator.Persistent);
    }

    private int Run(double now, float deadChance = 0f, int growDecayChance = 0, int catchUpMaxSteps = 0, int nearCapacity = -1)
    {
        var job = new NalDecideJob
        {
            Cell = _cell,
            Dense = _dense,
            Due = _due,
            Tier = _tier,
            Species = _species,
            Output = _output,
            Counter = _counter,
            Now = now,
            DeadChance = deadChance,
            Seed = 12345u,
            SecondsPerDelayUnit = 0.5f,
            FallbackDelaySeconds = 7f,
            GrowDecayChance = growDecayChance,
            CatchUpMaxSteps = catchUpMaxSteps,
            NearCapacity = nearCapacity < 0 ? _output.Length : nearCapacity,
            FarPeriodScale = 4f,
            DecayCheckSeconds = 30f
        };
        job.Execute(_cell.Length);
        return _counter[0];
    }

    [Test]
    public void SlotNotYetDueIsNotProcessed()
    {
        Setup(4, new[] { 10d }, Species(Nal, 100, 3, 2, 1, 0));

        Assert.AreEqual(0, Run(5d));
    }

    [Test]
    public void SlotExactlyDueIsProcessed()
    {
        Setup(4, new[] { 5d }, Species(Nal, 100, 1, 2, 1, 0));

        Assert.AreEqual(1, Run(5d));
        Assert.AreEqual(0, _output[0].Slot);
    }

    [Test]
    public void FullChanceWithEvolutionSpawnsEveryAttempt()
    {
        Setup(4, new[] { 0d }, Species(Nal, 100, 3, 4, 2, 1), Species(Nal, 0, 0, 0, 1, -1));
        _dense[0] = 0;

        Run(5d);

        NalDecision d = _output[0];
        Assert.AreEqual(3, d.SpawnCount);
        Assert.AreEqual(1, d.SpawnDense);
        for (int k = 0; k < d.SpawnCount; k++)
        {
            Assert.LessOrEqual(math.abs(d.TargetsX[k]), 4);
            Assert.LessOrEqual(math.abs(d.TargetsZ[k]), 4);
        }
    }

    [Test]
    public void SpawnCountIsCappedAtMaxSpawns()
    {
        Setup(4, new[] { 0d }, Species(Nal, 100, 50, 4, 1, 0));

        Run(1d);

        Assert.AreEqual(NalDecision.MaxSpawns, _output[0].SpawnCount);
    }

    [Test]
    public void ZeroChanceNeverSpawnsNorDies()
    {
        Setup(64, new double[64], Species(Nal, 0, 5, 4, 1, 0));

        int count = Run(1d);

        Assert.AreEqual(64, count);
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual(0, _output[i].SpawnCount);
            Assert.IsFalse(_output[i].DeathRoll);
        }
    }

    [Test]
    public void NoEvolutionNeverSpawns()
    {
        Setup(4, new[] { 0d }, Species(Nal, 100, 5, 4, 1, -1));

        Run(1d);

        Assert.AreEqual(0, _output[0].SpawnCount);
    }

    [Test]
    public void DeadChanceForcesDeathRoll()
    {
        Setup(16, new double[16], Species(Nal, 0, 0, 0, 1, -1));

        int count = Run(1d, deadChance: 100f);

        for (int i = 0; i < count; i++) Assert.IsTrue(_output[i].DeathRoll);
    }

    [Test]
    public void GrowWithoutEvolutionDiesByDecayChance()
    {
        Setup(16, new double[16], Species(Grow, 0, 0, 0, 1, -1));

        int count = Run(1d, growDecayChance: 100);
        for (int i = 0; i < count; i++) Assert.IsTrue(_output[i].DeathRoll);

        _counter[0] = 0;
        count = Run(1d, growDecayChance: 0);
        for (int i = 0; i < count; i++) Assert.IsFalse(_output[i].DeathRoll);
    }

    [Test]
    public void GrowWithEvolutionNeverRollsDeath()
    {
        Setup(16, new double[16], Species(Grow, 0, 0, 0, 1, 0));

        int count = Run(1d, growDecayChance: 100);

        for (int i = 0; i < count; i++)
        {
            Assert.IsFalse(_output[i].DeathRoll);
            Assert.AreEqual(0, _output[i].SpawnCount);
        }
    }

    [Test]
    public void NextDueUsesDelayUnitsAndClampsZeroToOne()
    {
        Setup(4, new[] { 0d, 0d }, Species(Nal, 0, 0, 0, 6, -1), Species(Nal, 0, 0, 0, 0, -1));
        _dense[1] = 1;

        Run(10d);

        for (int i = 0; i < 2; i++)
        {
            NalDecision d = _output[i];
            Assert.AreEqual(d.Slot == 0 ? 13d : 10.5d, d.NextDue, 1e-9);
        }
    }

    [Test]
    public void SpeciesWithoutDelayUsesFallbackSeconds()
    {
        NalSpeciesParams p = Species(Grow, 0, 0, 0, 0, -1);
        p.HasDelay = 0;
        Setup(4, new[] { 0d }, p);

        Run(10d);

        Assert.AreEqual(17d, _output[0].NextDue, 1e-9);
    }

    [Test]
    public void OutputOverflowCountsAllButWritesCapacity()
    {
        Setup(2, new double[5], Species(Nal, 0, 0, 0, 1, -1));

        int count = Run(1d);

        Assert.AreEqual(5, count);
        Assert.AreEqual(2, _output.Length);
    }

    [Test]
    public void LaggingSlotKeepsItsOwnCadence()
    {
        Setup(4, new[] { 95d }, Species(Nal, 0, 0, 0, 6, -1));

        Run(100d, catchUpMaxSteps: 4);

        Assert.AreEqual(98d, _output[0].NextDue, 1e-9);
    }

    [Test]
    public void LagIsClampedToCatchUpMaxSteps()
    {
        Setup(4, new[] { 0d }, Species(Nal, 0, 0, 0, 6, -1));

        Run(100d, catchUpMaxSteps: 4);

        Assert.AreEqual(94d, _output[0].NextDue, 1e-9);
    }

    [Test]
    public void CatchUpStepsNeverExceedLimit()
    {
        const int limit = 4;
        Setup(4, new[] { 0d }, Species(Nal, 0, 0, 0, 6, -1));

        int steps = 0;
        while (steps < 100)
        {
            _counter[0] = 0;
            if (Run(100d, catchUpMaxSteps: limit) == 0) break;
            _due[0] = _output[0].NextDue;
            steps++;
        }

        Assert.AreEqual(limit, steps);
    }

    [Test]
    public void ZeroCatchUpStepsKeepsSingleStep()
    {
        Setup(4, new[] { 0d }, Species(Nal, 0, 0, 0, 6, -1));

        int steps = 0;
        while (steps < 100)
        {
            _counter[0] = 0;
            if (Run(100d) == 0) break;
            _due[0] = _output[0].NextDue;
            steps++;
        }

        Assert.AreEqual(1, steps);
    }

    [Test]
    public void FarSlotsUseTheirOwnBudgetLane()
    {
        Setup(4, new double[6], Species(Nal, 0, 0, 0, 1, -1));
        for (int i = 0; i < 3; i++) _tier[i] = 1;

        Run(1d, nearCapacity: 2);

        Assert.AreEqual(3, _counter[0]);
        Assert.AreEqual(3, _counter[1]);
        Assert.GreaterOrEqual(_output[0].Slot, 3);
        Assert.GreaterOrEqual(_output[1].Slot, 3);
        Assert.Less(_output[2].Slot, 3);
        Assert.Less(_output[3].Slot, 3);
    }

    [Test]
    public void FarDecisionsLandInTheFarRegionOfTheOutput()
    {
        Setup(4, new[] { 0d, 0d }, Species(Nal, 0, 0, 0, 1, -1));
        _tier[1] = 2;

        Run(1d, nearCapacity: 3);

        Assert.AreEqual(1, _counter[0]);
        Assert.AreEqual(1, _counter[1]);
        Assert.AreEqual(0, _output[0].Slot);
        Assert.AreEqual(1, _output[3].Slot);
    }

    [Test]
    public void FarPeriodGrowsWithTier()
    {
        Setup(4, new[] { 0d, 0d }, Species(Nal, 0, 0, 0, 6, -1));
        _tier[1] = 2;

        Run(10d, nearCapacity: 3);

        Assert.AreEqual(13d, _output[0].NextDue, 1e-9);
        Assert.AreEqual(34d, _output[3].NextDue, 1e-9);
    }

    [Test]
    public void DecayKindDiesWhenPeriodDwarfsMeanLifetime()
    {
        NalSpeciesParams decay = Species(NalSpeciesParams.KindDecay, 0, 0, 0, 0, -1);
        decay.HasDelay = 0;
        decay.DecaySeconds = 0.001f;
        Setup(16, new double[16], decay);

        int count = Run(1d);

        Assert.AreEqual(16, count);
        for (int i = 0; i < count; i++) Assert.IsTrue(_output[i].DeathRoll);
    }

    [Test]
    public void DecayKindSurvivesWhenMeanLifetimeDwarfsPeriod()
    {
        NalSpeciesParams decay = Species(NalSpeciesParams.KindDecay, 0, 0, 0, 0, -1);
        decay.HasDelay = 0;
        decay.DecaySeconds = 1e9f;
        Setup(16, new double[16], decay);

        int count = Run(1d);

        Assert.AreEqual(16, count);
        for (int i = 0; i < count; i++) Assert.IsFalse(_output[i].DeathRoll);
    }

    [Test]
    public void DecayKindChecksAtDecayInterval()
    {
        NalSpeciesParams decay = Species(NalSpeciesParams.KindDecay, 0, 0, 0, 0, -1);
        decay.HasDelay = 0;
        decay.DecaySeconds = 1e9f;
        Setup(4, new[] { 0d }, decay);

        Run(10d);

        Assert.AreEqual(40d, _output[0].NextDue, 1e-9);
    }
}
