using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public class ColonyCanvasTests
{
    private SimulationRules _rules;
    private ColonyRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(_rules);
        _registry = new ColonyRegistry();
    }

    [TearDown]
    public void TearDown()
    {
        _registry.Dispose();
        Object.DestroyImmediate(_rules);
    }

    private ColonyState NewColony(int x, int memberIndex)
    {
        var colony = _registry.Add(new float2(x, 0f), null, 4);
        colony.AddMember(new EntityModel { ManagerIndex = memberIndex });
        return colony;
    }

    private static void Behave(ColonyState colony, int[] sequence, int goal, int rounds)
    {
        int previous = -1;
        for (int r = 0; r < rounds; r++)
            for (int i = 0; i < sequence.Length; i++)
            {
                colony.Stats.RecordAction(sequence[i]);
                colony.Stats.RecordHabit(previous, sequence[i], goal, 0.1f * i);
                previous = sequence[i];
            }
    }

    private static long Distance(Color32[] a, Color32[] b)
    {
        long sum = 0;
        for (int i = 0; i < a.Length; i++) sum += math.abs(a[i].r - b[i].r) + math.abs(a[i].g - b[i].g) + math.abs(a[i].b - b[i].b);
        return sum;
    }

    [Test]
    public void IdenticalDistributionsHaveZeroDivergenceAndDisjointOnesOneBit()
    {
        var p = new float[] { 1f, 2f, 3f, 0f };
        var same = new float[] { 2f, 4f, 6f, 0f };
        var disjoint = new float[] { 0f, 0f, 0f, 5f };

        Assert.AreEqual(0f, JensenShannon.Divergence(p, same), 1e-6f);
        Assert.AreEqual(1f, JensenShannon.Divergence(new float[] { 1f, 0f }, new float[] { 0f, 1f }), 1e-6f);
        Assert.AreEqual(JensenShannon.Divergence(p, disjoint), JensenShannon.Divergence(disjoint, p), 1e-6f);
        Assert.IsTrue(float.IsNaN(JensenShannon.Divergence(p, new float[4])));
    }

    [Test]
    public void ConditionalDivergenceWeighsRowsByTheirMeanShare()
    {
        var a = new float[] { 5f, 0f, 0f, 5f };
        var b = new float[] { 5f, 0f, 5f, 0f };
        Assert.AreEqual(0.5f, JensenShannon.Conditional(a, b, 2, 2), 1e-6f);
        Assert.AreEqual(0f, JensenShannon.Conditional(a, a, 2, 2), 1e-6f);
    }

    [Test]
    public void DifferentColoniesGiveDifferentCanvasesAndAColonyDiffersLessFromItsPreviousSample()
    {
        var a = NewColony(0, 0);
        var b = NewColony(100, 1);
        var canvas = new ColonyCanvas();

        canvas.Update(0f, _registry, null, "test");
        for (int sample = 1; sample <= 3; sample++)
        {
            Behave(a, new[] { 1, 3, 1, 3, 6 }, 2, 20);
            Behave(b, new[] { 2, 5, 2, 7, 9 }, 1, 20);
            canvas.Update(10f * sample, _registry, null, "test");
        }

        var slotA = canvas.SlotAt(a.Index);
        var slotB = canvas.SlotAt(b.Index);
        Assert.IsTrue(slotA.Rendered && slotA.HasPrevious);

        long between = Distance(slotA.Pixels, slotB.Pixels);
        long selfLag = Distance(slotA.Pixels, slotA.PreviousPixels);
        Assert.Greater(between, 0L);
        Assert.Less(selfLag, between);

        var pair = ColonyCanvas.Between(slotA.Current, slotB.Current);
        var self = ColonyCanvas.Between(slotA.Current, slotA.Previous);
        Assert.Greater(pair.ActionMix, self.ActionMix);
        Assert.Greater(pair.Bigram, self.Bigram);
        Assert.Greater(pair.GoalMix, self.GoalMix);
        Assert.AreEqual(0f, self.ActionMix, 1e-4f);
    }

    [Test]
    public void SameBehaviourGivesTheSameCanvasAndZeroDivergence()
    {
        var a = NewColony(0, 0);
        var b = NewColony(100, 1);
        var canvas = new ColonyCanvas();

        canvas.Update(0f, _registry, null, "test");
        Behave(a, new[] { 1, 3, 6 }, 2, 30);
        Behave(b, new[] { 1, 3, 6 }, 2, 30);
        canvas.Update(10f, _registry, null, "test");

        var slotA = canvas.SlotAt(a.Index);
        var slotB = canvas.SlotAt(b.Index);
        Assert.AreEqual(0L, Distance(slotA.Pixels, slotB.Pixels));
        var pair = ColonyCanvas.Between(slotA.Current, slotB.Current);
        Assert.AreEqual(0f, pair.ActionMix, 1e-6f);
        Assert.AreEqual(0f, pair.GoalMix, 1e-6f);
        Assert.AreEqual(0f, pair.Bigram, 1e-6f);
    }

    [Test]
    public void ActionDivergenceOfTheCanvasEqualsTheLogWindowDivergenceWithoutDecay()
    {
        typeof(SimulationRules).GetField("canvasDecay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(_rules, 0f);
        SimulationRules.Bind(_rules);

        var a = NewColony(0, 0);
        var b = NewColony(100, 1);
        var canvas = new ColonyCanvas();
        canvas.Update(0f, _registry, null, "test");

        Behave(a, new[] { 1, 3, 1, 6 }, 2, 10);
        Behave(b, new[] { 2, 3, 5, 6 }, 1, 10);
        canvas.Update(10f, _registry, null, "test");

        var window = new float[ActionCatalog.Count];
        var other = new float[ActionCatalog.Count];
        for (int i = 0; i < window.Length; i++)
        {
            window[i] = a.Stats.ActionsStarted[i];
            other[i] = b.Stats.ActionsStarted[i];
        }
        var pair = ColonyCanvas.Between(canvas.SlotAt(a.Index).Current, canvas.SlotAt(b.Index).Current);
        Assert.AreEqual(JensenShannon.Divergence(window, other), pair.ActionMix, 1e-5f);
    }

    [Test]
    public void HabitCountersFollowCommandStarts()
    {
        var colony = _registry.Add(new float2(0f, 0f), null, 4);
        colony.Stats.RecordHabit(-1, 3, 2, 0.5f);
        colony.Stats.RecordHabit(3, 6, 2, 0.99f);

        Assert.AreEqual(2, colony.Stats.GoalsStarted[2]);
        Assert.AreEqual(1, colony.Stats.ActionBigrams[3 * ActionCatalog.Count + 6]);
        Assert.AreEqual(0, colony.Stats.ActionBigrams[0]);
        Assert.AreEqual(1, colony.Stats.GoalPhase[2 * ColonyStats.PhaseBins + 25]);
        Assert.AreEqual(1, colony.Stats.GoalPhase[2 * ColonyStats.PhaseBins + 49]);
    }
}
