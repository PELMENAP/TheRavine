using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SignalMaskTests
{
    private SimulationRules _rules;

    [SetUp]
    public void SetUp() => _rules = ScriptableObject.CreateInstance<SimulationRules>();

    [TearDown]
    public void TearDown()
    {
        SimulationRules.Bind(null);
        Object.DestroyImmediate(_rules);
    }

    private void Set(string field, bool value)
    {
        var so = new SerializedObject(_rules);
        so.FindProperty(field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static VectorizerFrame Frame() => new VectorizerFrame
    {
        Health = 60f, Energy = 40f, DayPhase = 0.3f, Stress = 0.7f, Stomach = 0.4f,
        SpeechBag = new float[SimulationRules.SpeechVocab],
        PlanRewardEma = new float[PlanCatalog.Count],
        Predisposition = new[] { 0.1f, 0.2f, 0.3f },
    };

    private static float[] Vectorize(in VectorizerFrame frame)
    {
        var vectorizer = new InputVectorizer(100f, 100f);
        var times = new double[ActionCatalog.Count];
        return (float[])vectorizer.Vectorize(in frame, times, in TerrainSample.Invalid).Clone();
    }

    [Test]
    public void StressMaskChangesOnlyTheStressSlot()
    {
        var frame = Frame();

        SimulationRules.Bind(_rules);
        var open = Vectorize(in frame);

        Set("maskStressInput", true);
        SimulationRules.Bind(_rules);
        var masked = Vectorize(in frame);

        Assert.AreEqual(InputVectorizer.VectorSize, open.Length);
        Assert.AreEqual(open.Length, masked.Length);
        int slot = InputVectorizer.StateOffset + 12;
        Assert.AreEqual(0.7f, open[slot]);
        Assert.AreEqual(0f, masked[slot]);
        for (int i = 0; i < open.Length; i++)
            if (i != slot) Assert.AreEqual(open[i], masked[i], $"slot {i}");
    }

    [Test]
    public void SignalMasksReachTheFrame()
    {
        Set("maskStressInput", true);
        Set("maskTaskSignal", true);
        SimulationRules.Bind(_rules);
        Assert.IsTrue(SimulationRules.Frame.MaskStressInput);
        Assert.IsTrue(SimulationRules.Frame.MaskTaskSignal);
    }

    [Test]
    public void MaskedVectorizeDoesNotAllocate()
    {
        Set("maskStressInput", true);
        SimulationRules.Bind(_rules);
        var vectorizer = new InputVectorizer(100f, 100f);
        var frame = Frame();
        var times = new double[ActionCatalog.Count];
        float sink = 0f;

        TestDelegate step = () => sink += vectorizer.Vectorize(in frame, times, in TerrainSample.Invalid)[InputVectorizer.StateOffset];

        AllocationAssert.Zero(step, 200, "InputVectorizer.Vectorize with maskStressInput");
    }
}
