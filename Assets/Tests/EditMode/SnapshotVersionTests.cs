using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SnapshotVersionTests
{
    private SimulationRules _rules;
    private SharedHierarchicalBrain _brain;

    [SetUp]
    public void SetUp()
    {
        _rules = TestRules.Bind();
        _brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, 32);
    }

    [TearDown]
    public void TearDown()
    {
        _brain?.Dispose();
        ContextSlabs.DisposeAll();
        TestRules.Release(_rules);
    }

    [Test]
    public void RoundTripKeepsArchitecture()
    {
        byte[] data = _brain.ToSnapshot().Serialize();
        Assert.IsTrue(SharedBrainSnapshot.TryReadVersion(data, out int version));
        Assert.AreEqual(SharedBrainSnapshot.Version, version);

        var snapshot = SharedBrainSnapshot.Deserialize(data);
        Assert.NotNull(snapshot);
        var loaded = SharedHierarchicalBrain.FromSnapshot(snapshot, InputVectorizer.VectorSize, 32);
        Assert.NotNull(loaded);
        Assert.IsTrue(loaded.MatchesArchitecture(InputVectorizer.VectorSize, 32));
        loaded.Dispose();
    }

    [Test]
    public void StandardVersion12RemainsReadable()
    {
        byte[] current = _brain.ToSnapshot().Serialize();
        int oldHeader = sizeof(uint) + sizeof(int);
        byte[] old = new byte[current.Length - (SharedBrainSnapshot.HeaderSize - oldHeader)];
        Array.Copy(current, 0, old, 0, oldHeader);
        BitConverter.GetBytes(12).CopyTo(old, sizeof(uint));
        Array.Copy(current, SharedBrainSnapshot.HeaderSize, old, oldHeader, old.Length - oldHeader);

        var snapshot = SharedBrainSnapshot.Deserialize(old);
        Assert.NotNull(snapshot);
        using var model = snapshot.Brain;
        Assert.IsTrue(model.MatchesArchitecture(InputVectorizer.VectorSize, 32));
    }

    [Test]
    public void WideSocialSnapshotKeepsItsProfile()
    {
        using var brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, 32, true);
        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            int[] expected = SharedHierarchicalBrain.BuildExecSizes(InputVectorizer.VectorSize + 32, g);
            if (g == (int)SharedHierarchicalBrain.Goal.Social)
            {
                expected[1] = 96;
                expected[2] = 48;
                expected[3] = 48;
            }
            CollectionAssert.AreEqual(expected, brain.ExecLayerSizes[g]);
        }
        var snapshot = SharedBrainSnapshot.Deserialize(brain.ToSnapshot().Serialize());
        Assert.NotNull(snapshot);
        using var model = snapshot.Brain;
        Assert.IsTrue(model.MatchesArchitecture(InputVectorizer.VectorSize, 32, true));
        Assert.IsFalse(model.MatchesArchitecture(InputVectorizer.VectorSize, 32));
    }

    [Test]
    public void OtherVersionIsRefusedWithError()
    {
        byte[] data = _brain.ToSnapshot().Serialize();
        BitConverter.GetBytes(SharedBrainSnapshot.Version - 2).CopyTo(data, 4);

        LogAssert.Expect(LogType.Error, new Regex("версия .* загрузка отменена"));
        Assert.IsNull(SharedBrainSnapshot.Deserialize(data));
    }

    [Test]
    public void TruncatedDataIsRefusedWithError()
    {
        byte[] data = _brain.ToSnapshot().Serialize();
        Array.Resize(ref data, data.Length / 2);

        LogAssert.Expect(LogType.Error, new Regex("повреждённые данные"));
        Assert.IsNull(SharedBrainSnapshot.Deserialize(data));
    }

    [Test]
    public void UnknownFormatIsRefusedWithError()
    {
        var data = new byte[16];
        LogAssert.Expect(LogType.Error, new Regex("неизвестный формат"));
        Assert.IsNull(SharedBrainSnapshot.Deserialize(data));
        Assert.IsFalse(SharedBrainSnapshot.TryReadVersion(data, out _));
    }

    [Test]
    public void ArchitectureMismatchIsRefused()
    {
        var snapshot = SharedBrainSnapshot.Deserialize(_brain.ToSnapshot().Serialize());
        LogAssert.Expect(LogType.Error, new Regex("Архитектура снапшота"));
        Assert.IsNull(SharedHierarchicalBrain.FromSnapshot(snapshot, InputVectorizer.VectorSize, 16));
    }
}
