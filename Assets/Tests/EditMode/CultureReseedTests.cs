using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

public class CultureReseedTests
{
    private const int LstmHidden = 32;

    private SimulationRules _rules;

    [SetUp]
    public void SetUp()
    {
        SimulationClock.Reset();
        _rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(_rules);
        ContextSlabs.Reserve(8);
    }

    [TearDown]
    public void TearDown()
    {
        ContextSlabs.DisposeAll();
        Object.DestroyImmediate(_rules);
        SimulationClock.Reset();
    }

    private void SetField(string name, object value)
        => typeof(SimulationRules).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_rules, value);

    [Test]
    public void CopyHasZeroDistanceAndNoiseMovesItByTheRequestedAmount()
    {
        var a = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
        var copy = new SharedHierarchicalBrain(a);
        var noisy = new SharedHierarchicalBrain(a);
        var other = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
        try
        {
            Assert.AreEqual(0f, a.ExecutorCosineDistance(copy), 1e-5f);

            noisy.AddWeightNoise(0f);
            Assert.AreEqual(0f, a.ExecutorCosineDistance(noisy), 1e-5f);

            noisy.AddWeightNoise(0.3f);
            float small = a.ExecutorCosineDistance(noisy);
            Assert.Greater(small, 1e-4f);
            Assert.Less(small, 0.2f);

            Assert.Greater(a.ExecutorCosineDistance(other), 0.5f);
        }
        finally
        {
            a.Dispose();
            copy.Dispose();
            noisy.Dispose();
            other.Dispose();
        }
    }

    [Test]
    public void LargerWeightNoiseGivesLargerDistance()
    {
        var a = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
        var weak = new SharedHierarchicalBrain(a);
        var strong = new SharedHierarchicalBrain(a);
        try
        {
            weak.AddWeightNoise(0.1f);
            strong.AddWeightNoise(0.5f);
            Assert.Less(a.ExecutorCosineDistance(weak), a.ExecutorCosineDistance(strong));
        }
        finally
        {
            a.Dispose();
            weak.Dispose();
            strong.Dispose();
        }
    }

    [Test]
    public void FissionParentIsTheLargestColonyOfTheSameArmAboveTheThreshold()
    {
        var registry = new ColonyRegistry();
        try
        {
            SetField("armNullPolicyFraction", 1f);
            var otherArm = registry.Add(new float2(0f, 0f), null, 4);
            SetField("armNullPolicyFraction", 0f);
            var small = registry.Add(new float2(10f, 0f), null, 4);
            var large = registry.Add(new float2(20f, 0f), null, 4);
            var extinct = registry.Add(new float2(30f, 0f), null, 4);

            otherArm.Count((int)Caste.Worker, 100);
            small.Count((int)Caste.Worker, 20);
            large.Count((int)Caste.Worker, 20);
            large.Count((int)Caste.Soldier, 10);
            large.Count(ColonyState.JuvenileBucket, 50);

            Assert.AreSame(large, ColonyPopulation.PickFissionParent(registry, extinct, 24));
            Assert.IsNull(ColonyPopulation.PickFissionParent(registry, extinct, 30));
            Assert.IsNull(ColonyPopulation.PickFissionParent(registry, large, 30));
        }
        finally
        {
            registry.Dispose();
        }
    }

    [Test]
    public void ReplacingTheBrainRecordsTheDonorColony()
    {
        var registry = new ColonyRegistry();
        var first = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
        var second = new SharedHierarchicalBrain(InputVectorizer.VectorSize, LstmHidden);
        try
        {
            var colony = registry.Add(new float2(0f, 0f), first, 4);
            Assert.AreEqual(0, colony.BrainDonorId);
            Assert.AreEqual("fresh", colony.BrainOrigin);

            colony.ReplaceBrain(second, "reseeded", 7);
            Assert.AreEqual(7, colony.BrainDonorId);
            Assert.AreEqual("reseeded", colony.BrainOrigin);
        }
        finally
        {
            registry.Dispose();
            first.Dispose();
        }
    }

    [Test]
    public void CultureRulesDefaultToTheOldBehaviour()
    {
        Assert.AreEqual(0, _rules.ExtraCivilColonies);
        Assert.AreEqual(ReseedMode.CloneBest, _rules.ReseedMode);
        Assert.IsFalse(_rules.FissionEnabled);
    }
}
