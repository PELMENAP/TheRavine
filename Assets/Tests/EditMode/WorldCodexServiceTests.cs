using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Base;

public class WorldCodexServiceTests
{
    private const int K = (int)VarianceAxis.Count;

    private sealed class Purse : ICodexPurse
    {
        public readonly Dictionary<int, float> Energy = new();

        public float Balance(int authorId) => Energy.TryGetValue(authorId, out var e) ? e : 0f;

        public bool TryDebit(int authorId, float amount)
        {
            if (!(amount > 0f) || Balance(authorId) < amount) return false;
            Energy[authorId] -= amount;
            return true;
        }
    }

    private SimulationRules _baseRules;
    private SimulationRules _applied;
    private EnergyLedger _ledger;
    private Purse _purse;
    private WorldCodexService _codex;

    [SetUp]
    public void SetUp()
    {
        _baseRules = ScriptableObject.CreateInstance<SimulationRules>();
        _applied = _baseRules.CreateLawApplied();
        _ledger = new EnergyLedger();
        _purse = new Purse();
        _purse.Energy[1] = 100f;
        _purse.Energy[2] = 100f;
        _codex = new WorldCodexService(_applied, _ledger, _purse);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_applied);
        Object.DestroyImmediate(_baseRules);
    }

    private static float[] Delta(params (VarianceAxis axis, float value)[] entries)
    {
        var delta = new float[K];
        for (int i = 0; i < entries.Length; i++) delta[(int)entries[i].axis] = entries[i].value;
        return delta;
    }

    private int ProposeMetabolism(float value = 0.1f)
    {
        Assert.IsTrue(_codex.TryPropose(1, Delta((VarianceAxis.Metabolism, value)), out int id, out var error), error.ToString());
        return id;
    }

    [Test]
    public void ProposalDebitsOnceAndRecordsOutflow()
    {
        float cost = _applied.CodexProposeCost;
        ProposeMetabolism();

        Assert.AreEqual(100f - cost, _purse.Balance(1), 1e-5f);
        Assert.AreEqual(cost, _ledger.Flows.Codex, 1e-5f);
        Assert.AreEqual(cost, _ledger.Flows.Outflow, 1e-5f);
        Assert.AreEqual(1, _codex.LawCount);
    }

    [Test]
    public void RefusedProposalLeavesWorldUnchanged()
    {
        var cases = new (float[] delta, int author, CodexError expected)[]
        {
            (Delta((VarianceAxis.Metabolism, float.NaN)), 1, CodexError.BadValue),
            (new float[K], 1, CodexError.EmptyLaw),
            (Delta((VarianceAxis.Metabolism, 0.9f)), 1, CodexError.DeltaTooLarge),
            (Delta((VarianceAxis.Metabolism, 0.1f), (VarianceAxis.Aggression, 0.1f), (VarianceAxis.Mutation, 0.1f), (VarianceAxis.Juvenile, 0.1f)), 1, CodexError.TooManyAxes),
            (Delta((VarianceAxis.FoodGrowth, 0.3f)), 1, CodexError.Inadmissible),
            (Delta((VarianceAxis.Metabolism, 0.1f)), 99, CodexError.InsufficientEnergy),
            (Delta((VarianceAxis.Metabolism, 0.1f)), 0, CodexError.NoPurse),
            (new float[K - 1], 1, CodexError.BadValue),
        };

        for (int i = 0; i < cases.Length; i++)
        {
            bool ok = _codex.TryPropose(cases[i].author, cases[i].delta, out int id, out var error);
            Assert.IsFalse(ok, $"case {i}");
            Assert.AreEqual(cases[i].expected, error, $"case {i}");
            Assert.AreEqual(0, id);
        }

        Assert.AreEqual(100f, _purse.Balance(1));
        Assert.AreEqual(0f, _ledger.Flows.Codex);
        Assert.AreEqual(0, _codex.LawCount);
        Assert.AreEqual(0, _codex.Version);
    }

    [Test]
    public void BalancedLawIsAdmissibleByPrice()
    {
        var delta = Delta((VarianceAxis.FoodGrowth, 0.1f), (VarianceAxis.Metabolism, 0.1f));
        Assert.IsTrue(_codex.TryPropose(1, delta, out _, out var error), error.ToString());
    }

    [Test]
    public void LawWithSupportChangesFrameFieldsOfItsAxis()
    {
        float before = _applied.BasalEnergyDrain;
        float untouched = _applied.FoodGrowthRate;
        ProposeMetabolism(0.2f);

        Assert.AreEqual(before, _applied.BasalEnergyDrain, "laws apply only at the window boundary");
        _codex.Step(0f);

        float expected = before * math.exp(_codex.Theta[(int)VarianceAxis.Metabolism]);
        Assert.Greater(_applied.BasalEnergyDrain, before);
        Assert.AreEqual(expected, _applied.BasalEnergyDrain, before * 1e-5f);
        Assert.AreEqual(untouched, _applied.FoodGrowthRate);
        Assert.AreEqual(1, _codex.Version);
    }

    [Test]
    public void UnsupportedLawIsPrunedAndFrameReturnsToWorldVariant()
    {
        float before = _baseRules.BasalEnergyDrain;
        ProposeMetabolism(0.2f);
        _codex.Step(0f);
        Assert.AreNotEqual(before, _applied.BasalEnergyDrain);

        _codex.Step(1e7f);

        Assert.AreEqual(0, _codex.LawCount);
        Assert.AreEqual(before, _applied.BasalEnergyDrain);
        for (int k = 0; k < K; k++) Assert.AreEqual(0f, _codex.Theta[k]);
    }

    [Test]
    public void InvestmentDebitsOnceAndAddsQuadraticSupport()
    {
        int id = ProposeMetabolism();
        _codex.TryGetLaw(id, out var before);

        Assert.IsTrue(_codex.TryInvest(2, id, 16f, out var error), error.ToString());
        _codex.TryGetLaw(id, out var after);

        Assert.AreEqual(84f, _purse.Balance(2), 1e-5f);
        Assert.AreEqual(_applied.CodexProposeCost + 16f, _ledger.Flows.Codex, 1e-4f);
        Assert.AreEqual(CodexMath.VoteGain(16f, _applied.CodexVoteScale), after.Support - before.Support, 1e-5f);
    }

    [Test]
    public void RefusedInvestmentChargesNothing()
    {
        int id = ProposeMetabolism();
        float ledger = _ledger.Flows.Codex;

        Assert.IsFalse(_codex.TryInvest(2, id + 5, 10f, out var error));
        Assert.AreEqual(CodexError.NoSuchLaw, error);
        Assert.IsFalse(_codex.TryInvest(2, id, 1000f, out error));
        Assert.AreEqual(CodexError.InsufficientEnergy, error);
        Assert.IsFalse(_codex.TryInvest(2, id, _applied.CodexMinInvest * 0.5f, out error));
        Assert.AreEqual(CodexError.EnergyTooSmall, error);
        Assert.IsFalse(_codex.TryInvest(2, id, float.NaN, out error));
        Assert.AreEqual(CodexError.BadValue, error);
        Assert.IsFalse(_codex.TryOppose(2, K, 10f, out error));
        Assert.AreEqual(CodexError.BadAxis, error);

        Assert.AreEqual(100f, _purse.Balance(2));
        Assert.AreEqual(ledger, _ledger.Flows.Codex);
    }

    [Test]
    public void OppositionWeakensTheEffect()
    {
        int id = ProposeMetabolism(0.2f);
        _codex.TryInvest(2, id, 25f, out _);
        _codex.Step(0f);
        float supported = _codex.Theta[(int)VarianceAxis.Metabolism];

        Assert.IsTrue(_codex.TryOppose(2, (int)VarianceAxis.Metabolism, 49f, out var error), error.ToString());
        _codex.Step(0f);

        Assert.Less(_codex.Theta[(int)VarianceAxis.Metabolism], supported);
        Assert.Greater(_codex.Theta[(int)VarianceAxis.Metabolism], 0f);
    }

    [Test]
    public void StepDoesNotAllocateAfterWarmUp()
    {
        int id = ProposeMetabolism(0.2f);
        _codex.TryInvest(2, id, 25f, out _);
        _codex.TryOppose(2, (int)VarianceAxis.Aggression, 9f, out _);
        _codex.Step(0f);

        AllocationAssert.Zero(() => _codex.Step(0.5f), 50, "WorldCodexService.Step");
    }

    [Test]
    public void FullCodexRefusesWithoutCharging()
    {
        for (int i = 0; i < _applied.CodexMaxLaws; i++)
        {
            _purse.Energy[1] = 100f;
            Assert.IsTrue(_codex.TryPropose(1, Delta((VarianceAxis.Metabolism, 0.1f)), out _, out var e), e.ToString());
        }

        _purse.Energy[1] = 100f;
        float ledger = _ledger.Flows.Codex;
        Assert.IsFalse(_codex.TryPropose(1, Delta((VarianceAxis.Metabolism, 0.1f)), out _, out var error));
        Assert.AreEqual(CodexError.CodexFull, error);
        Assert.AreEqual(100f, _purse.Balance(1));
        Assert.AreEqual(ledger, _ledger.Flows.Codex);
    }

    [Test]
    public void PricesAreSampledOnlyAtWindowBoundaryFromFiniteFlows()
    {
        var flows = new EnergyFlows { FoodSpawn = 10f, Basal = 6f, Codex = 4f };
        var bad = new EnergyFlows { FoodSpawn = float.NaN };
        float window = _applied.CodexWindow;

        _codex.NoteEnergy(in flows);
        _codex.Step(window * 0.5f);
        Assert.AreEqual(0, _codex.PriceSamples);

        _codex.Step(window * 0.5f);
        Assert.AreEqual(1, _codex.PriceSamples);

        _codex.NoteEnergy(in bad);
        _codex.Step(window);
        Assert.AreEqual(1, _codex.PriceSamples);
        Assert.IsTrue(_codex.PricesFromPrior);
    }

    [Test]
    public void SaveRoundTripReproducesTheta()
    {
        int a = ProposeMetabolism(0.2f);
        _codex.TryPropose(2, Delta((VarianceAxis.FoodGrowth, 0.1f), (VarianceAxis.Metabolism, 0.1f)), out int b, out _);
        _codex.TryInvest(2, a, 30f, out _);
        _codex.TryOppose(1, (int)VarianceAxis.FoodGrowth, 16f, out _);
        _codex.Step(0f);

        var save = new CodexSave();
        _codex.Capture(save);
        var state = new WorldState { codex = save }.Clone();

        var rules2 = ScriptableObject.CreateInstance<SimulationRules>();
        var applied2 = rules2.CreateLawApplied();
        try
        {
            var loaded = new WorldCodexService(applied2, new EnergyLedger(), new Purse());
            Assert.IsTrue(loaded.Restore(state.codex));
            loaded.Step(0f);

            Assert.AreEqual(_codex.LawCount, loaded.LawCount);
            for (int k = 0; k < K; k++)
            {
                Assert.AreEqual(_codex.Theta[k], loaded.Theta[k], 1e-7f, $"theta {k}");
                Assert.AreEqual(_codex.Opposition[k], loaded.Opposition[k], 1e-7f, $"opposition {k}");
            }
            Assert.IsTrue(loaded.TryGetLaw(b, out var law));
            Assert.AreEqual(2, law.Author);
            Assert.AreEqual(_applied.BasalEnergyDrain, applied2.BasalEnergyDrain, 1e-7f);
            Assert.AreEqual(_applied.FoodGrowthRate, applied2.FoodGrowthRate, 1e-7f);

            var purse = new Purse();
            purse.Energy[1] = 100f;
            var next = new WorldCodexService(applied2, new EnergyLedger(), purse);
            next.Restore(state.codex);
            Assert.IsTrue(next.TryPropose(1, Delta((VarianceAxis.Metabolism, 0.1f)), out int fresh, out _));
            Assert.Greater(fresh, b);
        }
        finally
        {
            Object.DestroyImmediate(applied2);
            Object.DestroyImmediate(rules2);
        }
    }

    [Test]
    public void OldSaveWithoutCodexLoadsEmpty()
    {
        ProposeMetabolism(0.2f);
        _codex.Step(0f);

        var legacy = new WorldState();
        Assert.IsNull(legacy.codex);
        Assert.IsNull(legacy.Clone().codex);
        Assert.IsFalse(_codex.Restore(legacy.codex));

        _codex.Step(0f);
        Assert.AreEqual(0, _codex.LawCount);
        for (int k = 0; k < K; k++) Assert.AreEqual(0f, _codex.Theta[k]);
        Assert.AreEqual(_baseRules.BasalEnergyDrain, _applied.BasalEnergyDrain);
    }

    [Test]
    public void CorruptSaveLoadsEmpty()
    {
        ProposeMetabolism(0.2f);
        var save = new CodexSave();
        _codex.Capture(save);

        var broken = new[]
        {
            Mutate(save, s => s.lawSupports[0] = float.NaN),
            Mutate(save, s => s.lawSupports[0] = -1f),
            Mutate(save, s => s.lawDeltas[(int)VarianceAxis.Metabolism] = 5f),
            Mutate(save, s => s.lawDeltas = new float[3]),
            Mutate(save, s => s.axisCount = K + 1),
            Mutate(save, s => s.version = 99),
            Mutate(save, s => s.opposition = new float[2]),
            Mutate(save, s => s.lawIds[0] = 0),
            Mutate(save, s => s.nextLawId = 0),
        };

        for (int i = 0; i < broken.Length; i++)
        {
            Assert.IsFalse(_codex.Restore(broken[i]), $"case {i}");
            Assert.AreEqual(0, _codex.LawCount, $"case {i}");
        }
    }

    private static CodexSave Mutate(CodexSave source, System.Action<CodexSave> change)
    {
        var copy = source.Clone();
        change(copy);
        return copy;
    }
}
