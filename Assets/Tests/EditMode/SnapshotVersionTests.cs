using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SnapshotVersionTests
{
    private SharedHierarchicalBrain _brain;

    [SetUp]
    public void SetUp() => _brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, 32);

    [TearDown]
    public void TearDown()
    {
        _brain.Dispose();
        ContextSlabs.DisposeAll();
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
    public void OtherVersionIsRefusedWithError()
    {
        byte[] data = _brain.ToSnapshot().Serialize();
        BitConverter.GetBytes(SharedBrainSnapshot.Version - 1).CopyTo(data, 4);

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
