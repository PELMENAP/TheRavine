using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class AtomicFileTests
{
    private string _directory;
    private string _path;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Application.temporaryCachePath, "atomic_file_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "model.bin");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void WriteCreatesAndReplacesTheFileWithoutLeavingATemp()
    {
        AtomicFile.WriteAllBytes(_path, new byte[] { 1, 2, 3 });
        AtomicFile.WriteAllBytes(_path, new byte[] { 9, 8 });

        Assert.AreEqual(new byte[] { 9, 8 }, File.ReadAllBytes(_path));
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    }

    [Test]
    public void FailureMidWriteKeepsThePreviousFile()
    {
        var previous = new byte[] { 1, 2, 3, 4 };
        AtomicFile.WriteAllBytes(_path, previous);

        Assert.Throws<IOException>(() => AtomicFile.Write(_path, stream =>
        {
            stream.Write(new byte[] { 7, 7 }, 0, 2);
            throw new IOException("disk full");
        }));

        Assert.AreEqual(previous, File.ReadAllBytes(_path));
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    }

    [Test]
    public void FailureOfTheFirstWriteLeavesNoFile()
    {
        Assert.Throws<InvalidOperationException>(() => AtomicFile.Write(_path, stream => throw new InvalidOperationException()));

        Assert.IsFalse(File.Exists(_path));
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    }

    [Test]
    public void MissingDirectoryThrowsAndCreatesNothing()
    {
        string missing = Path.Combine(_directory, "nope", "model.bin");

        Assert.Throws<DirectoryNotFoundException>(() => AtomicFile.WriteAllBytes(missing, new byte[] { 1 }));
        Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "nope")));
    }

    [Test]
    public void StaleTempFromACrashDoesNotBlockTheNextWrite()
    {
        File.WriteAllBytes(_path + ".tmp", new byte[] { 5, 5, 5, 5, 5, 5, 5, 5 });

        AtomicFile.WriteAllBytes(_path, new byte[] { 1 });

        Assert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(_path));
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    }

    [Test]
    public void ReadRefusesFilesOverTheLimitBeforeReading()
    {
        File.WriteAllBytes(_path, new byte[100]);

        Assert.IsFalse(AtomicFile.TryReadAllBytes(_path, 99, out var tooBig));
        Assert.IsNull(tooBig);
        Assert.IsTrue(AtomicFile.TryReadAllBytes(_path, 100, out var ok));
        Assert.AreEqual(100, ok.Length);
        Assert.IsFalse(AtomicFile.TryReadAllBytes(_path + ".missing", 100, out var missing));
        Assert.IsNull(missing);
        Assert.IsFalse(AtomicFile.IsWithinLimit(_path, 99));
        Assert.IsTrue(AtomicFile.IsWithinLimit(_path, 100));
    }

    [UnityTest]
    public IEnumerator AsyncWriteReplacesTheFile() => UniTask.ToCoroutine(async () =>
    {
        await AtomicFile.WriteAllBytesAsync(_path, new byte[] { 1, 2, 3 });
        await AtomicFile.WriteAllBytesAsync(_path, new byte[] { 4 });

        Assert.AreEqual(new byte[] { 4 }, File.ReadAllBytes(_path));
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    });

    [UnityTest]
    public IEnumerator CancelledAsyncWriteKeepsThePreviousFile() => UniTask.ToCoroutine(async () =>
    {
        var previous = new byte[] { 1, 2, 3 };
        AtomicFile.WriteAllBytes(_path, previous);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        OperationCanceledException caught = null;
        try { await AtomicFile.WriteAllBytesAsync(_path, new byte[] { 9, 9, 9, 9 }, cts.Token); }
        catch (OperationCanceledException ex) { caught = ex; }

        Assert.NotNull(caught);
        Assert.AreEqual(previous, File.ReadAllBytes(_path));
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    });
}
