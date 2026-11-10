using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

public class ModelLoaderFuzzTests
{
    private const int LstmInput = 8;
    private const int LstmHidden = 4;
    private static readonly int[] PerceptronSizes = { 6, 4, 3, 3, 2 };

    private SimulationRules _rules;

    [SetUp]
    public void SetUp()
    {
        _rules = TestRules.Bind();
        LogAssert.ignoreFailingMessages = true;
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
        ContextSlabs.DisposeAll();
        TestRules.Release(_rules);
    }

    private static byte[] LstmBytes()
    {
        using var lstm = new LSTMMemory(LstmInput, LstmHidden);
        return lstm.Serialize();
    }

    private static byte[] PerceptronBytes()
    {
        using var mlp = new DelayedPerceptron(PerceptronSizes);
        return mlp.Serialize();
    }

    private static byte[] Header(params int[] values)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        foreach (var v in values) bw.Write(v);
        return ms.ToArray();
    }

    private static void Patch(byte[] data, int offset, float value)
    {
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static long Measure(Action action)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Test]
    public void LstmRoundTripAndExpectedInputAreAccepted()
    {
        var data = LstmBytes();

        using var plain = LSTMMemory.Deserialize(data);
        using var expected = LSTMMemory.Deserialize(data, LstmInput);

        Assert.NotNull(plain);
        Assert.NotNull(expected);
        Assert.Throws<InvalidDataException>(() => LSTMMemory.Deserialize(data, LstmInput + 1));
    }

    [Test]
    public void LstmHeaderDimensionsAreBoundedBeforeAllocation()
    {
        int[][] headers =
        {
            new[] { int.MaxValue, int.MaxValue },
            new[] { int.MinValue, 4 },
            new[] { 0, 4 },
            new[] { 8, 0 },
            new[] { -1, -1 },
            new[] { LSTMMemory.MaxInputSize + 1, 4 },
            new[] { 8, LSTMMemory.MaxHiddenSize + 1 },
            new[] { LSTMMemory.MaxInputSize, LSTMMemory.MaxHiddenSize },
        };

        foreach (var h in headers)
        {
            var data = Header(h[0], h[1], 0, 0);
            long bytes = Measure(() => Assert.Throws<InvalidDataException>(() => LSTMMemory.Deserialize(data), $"{h[0]}/{h[1]}"));
            Assert.Less(bytes, 1 << 20, $"{h[0]}/{h[1]}: {bytes} байт");
        }
    }

    [Test]
    public void LstmTruncatedOrPaddedDataIsRejected()
    {
        var data = LstmBytes();

        for (int len = 0; len < data.Length; len += 7)
        {
            var cut = new byte[len];
            Array.Copy(data, cut, len);
            Assert.Throws<InvalidDataException>(() => LSTMMemory.Deserialize(cut), $"len {len}");
        }

        var padded = new byte[data.Length + 1];
        Array.Copy(data, padded, data.Length);
        Assert.Throws<InvalidDataException>(() => LSTMMemory.Deserialize(padded));
        Assert.Throws<InvalidDataException>(() => LSTMMemory.Deserialize(null));
    }

    [Test]
    public void LstmNonFiniteWeightsAreRejected()
    {
        float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        int weightsOffset = 8 + 4;
        int biasOffset = weightsOffset + 4 * 4 * LstmHidden * (LstmInput + LstmHidden) + 4;

        foreach (var v in bad)
        {
            var inWeights = LstmBytes();
            Patch(inWeights, weightsOffset + 12, v);
            Assert.Throws<InvalidDataException>(() => LSTMMemory.Deserialize(inWeights), $"W {v}");

            var inBias = LstmBytes();
            Patch(inBias, biasOffset, v);
            Assert.Throws<InvalidDataException>(() => LSTMMemory.Deserialize(inBias), $"b {v}");
        }
    }

    [Test]
    public void PerceptronRoundTripAndExpectedSizesAreAccepted()
    {
        var data = PerceptronBytes();

        using var plain = DelayedPerceptron.Deserialize(data);
        using var expected = DelayedPerceptron.Deserialize(data, PerceptronSizes);

        Assert.NotNull(plain);
        Assert.NotNull(expected);
        Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(data, new[] { 6, 4, 3, 3, 3 }));
        Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(data, new[] { 6, 4, 3, 3 }));
    }

    [Test]
    public void PerceptronHeaderDimensionsAreBoundedBeforeAllocation()
    {
        int[][] headers =
        {
            new[] { 5, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue },
            new[] { 5, 6, 4, 3, 3, -1 },
            new[] { 5, 0, 4, 3, 3, 2 },
            new[] { 5, 6, 4, 3, 3, DelayedPerceptron.MaxLayerSize + 1 },
            new[] { 5, 4096, 4096, 4096, 4096, 4096 },
            new[] { int.MaxValue },
            new[] { -5 },
            new[] { 4, 1, 1, 1, 1 },
            new[] { 6, 1, 1, 1, 1, 1, 1 },
        };

        foreach (var h in headers)
        {
            var data = Header(h);
            long bytes = Measure(() => Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(data), string.Join(",", h)));
            Assert.Less(bytes, 1 << 20, $"{string.Join(",", h)}: {bytes} байт");
        }
    }

    [Test]
    public void PerceptronTruncatedOrPaddedDataIsRejected()
    {
        var data = PerceptronBytes();

        for (int len = 0; len < data.Length; len += 11)
        {
            var cut = new byte[len];
            Array.Copy(data, cut, len);
            Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(cut), $"len {len}");
        }

        var padded = new byte[data.Length + 1];
        Array.Copy(data, padded, data.Length);
        Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(padded));
        Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(null));
    }

    [Test]
    public void PerceptronNonFiniteWeightsAreRejected()
    {
        float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        int firstWeight = 4 + 4 * PerceptronSizes.Length + 4;

        foreach (var v in bad)
        {
            var data = PerceptronBytes();
            Patch(data, firstWeight, v);
            Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(data), $"{v}");

            var last = PerceptronBytes();
            Patch(last, last.Length - 4, v);
            Assert.Throws<InvalidDataException>(() => DelayedPerceptron.Deserialize(last), $"last {v}");
        }
    }

    [Test]
    public void RandomBytesNeverEscapeAsOtherExceptions()
    {
        var rng = new System.Random(12345);

        for (int i = 0; i < 300; i++)
        {
            var data = new byte[rng.Next(0, 4096)];
            rng.NextBytes(data);

            try { LSTMMemory.Deserialize(data)?.Dispose(); } catch (InvalidDataException) { }
            try { DelayedPerceptron.Deserialize(data)?.Dispose(); } catch (InvalidDataException) { }
            Assert.IsNull(SharedBrainSnapshot.Deserialize(data));
        }
    }

    [Test]
    public void SnapshotWithBadModelBlocksIsRefused()
    {
        using var brain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, 32);
        var good = brain.ToSnapshot().Serialize();
        int lstmBlock = 12;
        int firstWeight = lstmBlock + 8 + 4;

        var nan = (byte[])good.Clone();
        Patch(nan, firstWeight, float.NaN);
        Assert.IsNull(SharedBrainSnapshot.Deserialize(nan));

        var wrongInput = (byte[])good.Clone();
        BitConverter.GetBytes(InputVectorizer.VectorSize + 1).CopyTo(wrongInput, lstmBlock);
        Assert.IsNull(SharedBrainSnapshot.Deserialize(wrongInput));

        var hugeInput = (byte[])good.Clone();
        BitConverter.GetBytes(int.MaxValue).CopyTo(hugeInput, lstmBlock);
        BitConverter.GetBytes(int.MaxValue).CopyTo(hugeInput, lstmBlock + 4);
        long bytes = Measure(() => Assert.IsNull(SharedBrainSnapshot.Deserialize(hugeInput)));
        Assert.Less(bytes, 1 << 20);

        var snapshot = SharedBrainSnapshot.Deserialize(good);
        Assert.NotNull(snapshot);
        snapshot.Brain.Dispose();
    }
}
