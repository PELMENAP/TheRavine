using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RavineLoggerTests
{
    private string _fileName;
    private RavineLogger _logger;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _fileName = "logger_test_" + Guid.NewGuid().ToString("N") + ".txt";
        _logger = new RavineLogger(null, _fileName);
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
        string path = Path.Combine(Application.persistentDataPath, _fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    [Test]
    public void LoggedErrorsAreBoundedByTheRingBuffer()
    {
        for (int i = 0; i < RavineLogger.MaxLoggedErrors + 1; i++)
            _logger.LogError("distinct error " + i);

        Assert.AreEqual(RavineLogger.MaxLoggedErrors, _logger.LoggedErrorCount);

        for (int i = 0; i < RavineLogger.MaxLoggedErrors * 4; i++)
            _logger.LogError("another distinct error " + i);

        Assert.AreEqual(RavineLogger.MaxLoggedErrors, _logger.LoggedErrorCount);
    }

    [Test]
    public void RepeatedMessageIsStoredOnce()
    {
        _logger.LogError("same");
        _logger.LogError("same");
        _logger.LogError("same");

        Assert.AreEqual(1, _logger.LoggedErrorCount);
    }
}
