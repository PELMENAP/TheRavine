using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BrainArchitectureVersionTests
{
    private const int ExpectedVersion          = 7;
    private const int ExpectedInputSize        = 105;
    private const int ExpectedPlanCount        = 9;
    private const int ExpectedCoordAux         = 2;
    private const int ExpectedActionCount      = 20;
    private const int ExpectedGoalCount        = 4;
    private const int ExpectedSpeechOutputs    = 4;
    private const int ExpectedDirectionOutputs = 8;
    private const int ExpectedLstmHidden       = 32;
    private const int ExpectedLstmWeights      = 4 * ExpectedLstmHidden * (ExpectedInputSize + ExpectedLstmHidden);
    private const int ExpectedLstmBiases       = 4 * ExpectedLstmHidden;

    private static readonly int[] ExpectedCoordLayers = { 137, 32, 16, 16, 12 };

    private static readonly int[][] ExpectedExecLayers =
    {
        new[] { 137, 64, 32, 32, 18 },
        new[] { 137, 64, 32, 32, 9 },
        new[] { 137, 64, 32, 32, 18 },
        new[] { 137, 64, 32, 32, 15 },
    };

    private static readonly int[] ExpectedSubsetSizes = { 8, 5, 8, 7 };
    private static readonly int[] ExpectedExecAux     = { 9, 3, 9, 7 };

    private static string Bump(string what)
        => $"{what} изменился: инкрементируйте SharedBrainSnapshot.Version (сейчас {SharedBrainSnapshot.Version}) "
         + "и обновите ожидаемые константы в BrainArchitectureVersionTests. Сохранённые мозги станут несовместимы.";

    [Test]
    public void VersionMatchesRecordedArchitecture()
        => Assert.AreEqual(ExpectedVersion, SharedBrainSnapshot.Version,
            "SharedBrainSnapshot.Version изменён: обновите ExpectedVersion и ожидаемые размеры в тесте");

    [Test]
    public void InputSizeUnchanged()
        => Assert.AreEqual(ExpectedInputSize, InputVectorizer.VectorSize, Bump("InputVectorizer.VectorSize"));

    [Test]
    public void CatalogSizesUnchanged()
    {
        Assert.AreEqual(ExpectedPlanCount, PlanCatalog.Count, Bump("PlanCatalog.Count"));
        Assert.AreEqual(ExpectedCoordAux, SharedHierarchicalBrain.CoordAux, Bump("SharedHierarchicalBrain.CoordAux"));
        Assert.AreEqual(ExpectedActionCount, ActionCatalog.Count, Bump("ActionCatalog.Count"));
        Assert.AreEqual(ExpectedGoalCount, SharedHierarchicalBrain.GoalCount, Bump("SharedHierarchicalBrain.GoalCount"));
        Assert.AreEqual(ExpectedSpeechOutputs, SharedHierarchicalBrain.SpeechOutputs, Bump("SpeechSignal.Count"));
        Assert.AreEqual(ExpectedDirectionOutputs, SharedHierarchicalBrain.DirectionOutputs, Bump("DirectionKind.Count"));
    }

    [Test]
    public void ExecutorHeadsUnchanged()
    {
        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            Assert.AreEqual(ExpectedSubsetSizes[g], SharedHierarchicalBrain.ActionSubsets[g].Length, Bump($"ActionSubsets[{g}]"));
            Assert.AreEqual(ExpectedExecAux[g], SharedHierarchicalBrain.ExecAux(g), Bump($"ExecAux({g})"));
        }
    }

    [Test]
    public void PerceptronLayersUnchanged()
    {
        int combined = ExpectedInputSize + ExpectedLstmHidden;
        CollectionAssert.AreEqual(ExpectedCoordLayers, SharedHierarchicalBrain.BuildCoordSizes(combined), Bump("Слои координатора"));

        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
            CollectionAssert.AreEqual(ExpectedExecLayers[g], SharedHierarchicalBrain.BuildExecSizes(combined, g), Bump($"Слои исполнителя {g}"));
    }

    [Test]
    public void LstmHiddenUnchanged()
    {
        var ctor = typeof(SharedHierarchicalBrain).GetConstructor(new[] { typeof(int), typeof(int) });
        Assert.NotNull(ctor);
        Assert.AreEqual(ExpectedLstmHidden, (int)ctor.GetParameters()[1].DefaultValue, Bump("SharedHierarchicalBrain lstmHidden по умолчанию"));

        var go = new GameObject("EntityManagerProbe");
        try
        {
            var manager = go.AddComponent<EntityManager>();
            var field   = typeof(EntityManager).GetField("lstmHidden", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.AreEqual(ExpectedLstmHidden, (int)field.GetValue(manager), Bump("EntityManager.lstmHidden"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LstmLayoutUnchanged()
    {
        var lstm = new LSTMMemory(InputVectorizer.VectorSize, ExpectedLstmHidden);
        try
        {
            int expectedBytes = 4 * 4 + 4 * (ExpectedLstmWeights + ExpectedLstmBiases);
            Assert.AreEqual(expectedBytes, lstm.Serialize().Length, Bump("Размер слоёв LSTM"));
        }
        finally
        {
            lstm.Dispose();
        }
    }
}
