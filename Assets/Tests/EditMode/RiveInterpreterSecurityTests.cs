using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TheRavine.Base;
using UnityEngine.TestTools;

public class RiveInterpreterSecurityTests
{
    private RavineLogger _logger;
    private RiveRuntime _runtime;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _logger = new RavineLogger(null);
        _runtime = new RiveRuntime(null, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
    }

    private void Load(string name, string source) => _runtime.LoadFile(name, source);

    [UnityTest]
    public IEnumerator InterleavedRunsKeepIsolatedVariables() => UniTask.ToCoroutine(async () =>
    {
        Load("run_a", "int x = 1\nint g = get\n>> x");
        Load("run_b", "int x = 2\nint g = get\n>> x");

        var ta = _runtime.ExecuteScriptAsync("run_a").Preserve();
        var tb = _runtime.ExecuteScriptAsync("run_b").Preserve();
        Assert.AreEqual(2, _runtime.GetWaitingInputReaders());

        _runtime.PushInput(0);
        var ra = await ta;
        var rb = await tb;

        Assert.IsTrue(ra.Success, ra.ErrorMessage);
        Assert.IsTrue(rb.Success, rb.ErrorMessage);
        Assert.AreEqual(1, ra.ReturnValue);
        Assert.AreEqual(2, rb.ReturnValue);
    });

    [UnityTest]
    public IEnumerator CalleeDoesNotOverwriteCallerVariable() => UniTask.ToCoroutine(async () =>
    {
        Load("callee_write", "(a)\nint x = 99\n>> a");
        Load("caller_write", "int x = 5\nint r = callee_write(1)\n>> x");

        var result = await _runtime.ExecuteScriptAsync("caller_write");

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(5, result.ReturnValue);
    });

    [UnityTest]
    public IEnumerator CalleeDoesNotReadCallerVariable() => UniTask.ToCoroutine(async () =>
    {
        Load("callee_read", ">> x");
        Load("caller_read", "int x = 7\nint r = callee_read()\n>> r");

        var result = await _runtime.ExecuteScriptAsync("caller_read");

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(0, result.ReturnValue);
    });

    [UnityTest]
    public IEnumerator RunsKeepIndependentOperationCounters() => UniTask.ToCoroutine(async () =>
    {
        var body = new System.Text.StringBuilder("int a = 1\nint g = get\n");
        for (int i = 0; i < 20; i++) body.Append("a = a + 1\n");
        body.Append(">> a");

        var program = new RiveParser(_logger).Parse("counted", body.ToString());
        var inputs = new InputStreamManager();
        var executor = new RiveExecutor(_runtime, inputs, new InteractorRegistry(_logger));

        var exhausted = new RiveRun { OperationCount = 9995 };
        var fresh = new RiveRun();

        var te = executor.ExecuteAsync(program, Array.Empty<int>(), exhausted).Preserve();
        var tf = executor.ExecuteAsync(program, Array.Empty<int>(), fresh).Preserve();
        Assert.AreEqual(2, inputs.GetWaitingReadersCount());

        inputs.PushInput(0);
        var re = await te;
        var rf = await tf;

        Assert.IsFalse(re.Success);
        StringAssert.Contains("Operation limit", re.ErrorMessage);
        Assert.IsTrue(rf.Success, rf.ErrorMessage);
        Assert.AreEqual(21, rf.ReturnValue);
    });

    [UnityTest]
    public IEnumerator CalleeSharesOperationBudgetWithCaller() => UniTask.ToCoroutine(async () =>
    {
        var callee = new System.Text.StringBuilder();
        for (int i = 0; i < 20; i++) callee.Append("int t = 1\n");
        callee.Append(">> 1");
        Load("budget_callee", callee.ToString());

        var program = new RiveParser(_logger).Parse("budget_caller", "int r = budget_callee()\n>> r");
        var executor = new RiveExecutor(_runtime, new InputStreamManager(), new InteractorRegistry(_logger));

        var result = await executor.ExecuteAsync(program, Array.Empty<int>(), new RiveRun { OperationCount = 9985 });

        Assert.IsFalse(result.Success);
        StringAssert.Contains("Operation limit", result.ErrorMessage);
    });

    private static int ReferencePow(int b, int e)
    {
        int r = 1;
        for (int i = 0; i < e; i++) r = unchecked(r * b);
        return r;
    }

    [UnityTest]
    public IEnumerator PowWithHugeExponentReturnsImmediately() => UniTask.ToCoroutine(async () =>
    {
        var sw = Stopwatch.StartNew();
        var max = await _runtime.ExecuteScriptAsync("pow", 2, int.MaxValue);
        var odd = await _runtime.ExecuteScriptAsync("pow", 3, int.MaxValue - 1);
        sw.Stop();

        Assert.IsTrue(max.Success, max.ErrorMessage);
        Assert.IsTrue(odd.Success, odd.ErrorMessage);
        Assert.AreEqual(0, max.ReturnValue);
        Assert.Less(sw.ElapsedMilliseconds, 200);
    });

    [UnityTest]
    public IEnumerator PowMatchesRepeatedMultiplicationForSmallArguments() => UniTask.ToCoroutine(async () =>
    {
        int[] bases = { -3, -2, -1, 0, 1, 2, 3, 5, 10, 123456789, int.MaxValue, int.MinValue };
        foreach (var b in bases)
        {
            for (int e = 0; e <= 70; e++)
            {
                var result = await _runtime.ExecuteScriptAsync("pow", b, e);
                Assert.IsTrue(result.Success, result.ErrorMessage);
                Assert.AreEqual(ReferencePow(b, e), result.ReturnValue, $"pow({b}, {e})");
            }
        }
    });

    [UnityTest]
    public IEnumerator PowRejectsNegativeExponent() => UniTask.ToCoroutine(async () =>
    {
        var result = await _runtime.ExecuteScriptAsync("pow", 2, -1);
        Assert.IsFalse(result.Success);
    });

    [UnityTest]
    public IEnumerator AbsOfIntMinValueWrapsWithoutException() => UniTask.ToCoroutine(async () =>
    {
        var result = await _runtime.ExecuteScriptAsync("abs", int.MinValue);
        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(int.MinValue, result.ReturnValue);
    });

    private RiveParseException ParseFails(string source)
    {
        return Assert.Throws<RiveParseException>(() => new RiveParser(_logger).Parse("malicious", source));
    }

    [Test]
    public void DeeplyNestedParenthesesAreRejected()
    {
        var source = "int x = " + new string('(', 1000) + "1" + new string(')', 1000);
        var ex = ParseFails(source);
        StringAssert.Contains("Nesting", ex.Message);
    }

    [Test]
    public void TenThousandNestedParenthesesAreRejected()
    {
        ParseFails("int x = " + new string('(', 10000) + "1" + new string(')', 10000));
    }

    [Test]
    public void DeeplyNestedUnaryOperatorsAreRejected()
    {
        var ex = ParseFails("int x = " + new string('-', 10000) + "1");
        StringAssert.Contains("Nesting", ex.Message);
    }

    [Test]
    public void DeeplyNestedBlocksAreRejected()
    {
        var source = new System.Text.StringBuilder();
        for (int i = 0; i < 1000; i++) source.Append("if 1\n");
        for (int i = 0; i < 1000; i++) source.Append("end\n");

        var ex = ParseFails(source.ToString());
        StringAssert.Contains("Nesting", ex.Message);
    }

    [Test]
    public void TenThousandNestedIfBlocksAreRejected()
    {
        var source = new System.Text.StringBuilder();
        for (int i = 0; i < 10000; i++) source.Append("if 1\n");
        for (int i = 0; i < 10000; i++) source.Append("end\n");

        ParseFails(source.ToString());
    }

    [Test]
    public void OversizedSourceIsRejectedBeforeLexing()
    {
        var ex = ParseFails(new string('a', RiveParser.MaxSourceLength + 1));
        StringAssert.Contains("characters", ex.Message);
    }

    [Test]
    public void TooManyTokensAreRejected()
    {
        var ex = ParseFails("int x = 1" + string.Concat(System.Linq.Enumerable.Repeat("+1", RiveParser.MaxTokens)));
        StringAssert.Contains("tokens", ex.Message);
    }

    [Test]
    public void OversizedArrayLiteralIsRejected()
    {
        var items = string.Join(",", System.Linq.Enumerable.Repeat("1", RiveParser.MaxArrayLiteralLength + 1));
        var ex = ParseFails($"int a = [{items}]");
        StringAssert.Contains("Array literal", ex.Message);
    }

    [Test]
    public void TooManyCallArgumentsAreRejected()
    {
        var items = string.Join(",", System.Linq.Enumerable.Repeat("1", RiveParser.MaxCallArguments + 1));
        var ex = ParseFails($"int a = max({items})");
        StringAssert.Contains("arguments", ex.Message);
    }

    [Test]
    public void TooManyParametersAreRejected()
    {
        var names = string.Join(",", System.Linq.Enumerable.Range(0, RiveParser.MaxCallArguments + 1).Select(i => "p" + i));
        ParseFails($"({names})\n>> 1");
    }

    [Test]
    public void OutOfRangeNumberIsParseErrorNotOverflow()
    {
        ParseFails("int x = 99999999999");
    }

    [Test]
    public void UnexpectedCharacterOnFirstParseIsParseError()
    {
        ParseFails("int x = $");
    }

    [Test]
    public void SourceAtTheLimitsStillParses()
    {
        var elements = string.Join(",", System.Linq.Enumerable.Repeat("1", RiveParser.MaxArrayLiteralLength));
        var args = string.Join(",", System.Linq.Enumerable.Repeat("1", RiveParser.MaxCallArguments));
        var source = $"int a = [{elements}]\nint b = max({args})\nint c = {new string('(', 30)}1{new string(')', 30)}\n>> c";

        var program = new RiveParser(_logger).Parse("limits", source);

        Assert.AreEqual(3 + 1, program.Statements.Count);
    }

    [Test]
    public void LoadFileWithMaliciousSourceDoesNotThrowAndDoesNotLoad()
    {
        Assert.DoesNotThrow(() => _runtime.LoadFile("huge", new string('(', 10000)));
        Assert.IsFalse(_runtime.IsFileLoaded("huge"));

        Assert.DoesNotThrow(() => _runtime.LoadFile("long", new string('a', RiveParser.MaxSourceLength + 1)));
        Assert.IsFalse(_runtime.IsFileLoaded("long"));
    }

    private RiveExecutor NewExecutor(InputStreamManager inputs = null)
    {
        return new RiveExecutor(_runtime, inputs ?? new InputStreamManager(), new InteractorRegistry(_logger));
    }

    [UnityTest]
    public IEnumerator InputReadTimesOut() => UniTask.ToCoroutine(async () =>
    {
        var inputs = new InputStreamManager();
        TimeoutException caught = null;
        try { await inputs.GetAsync(CancellationToken.None, 50); }
        catch (TimeoutException ex) { caught = ex; }

        Assert.NotNull(caught);
        Assert.AreEqual(0, inputs.GetWaitingReadersCount());
    });

    [UnityTest]
    public IEnumerator InputReadIsCancelledByToken() => UniTask.ToCoroutine(async () =>
    {
        var inputs = new InputStreamManager();
        using var cts = new CancellationTokenSource();
        var pending = inputs.GetAsync(cts.Token, 60000).Preserve();
        Assert.AreEqual(1, inputs.GetWaitingReadersCount());

        cts.Cancel();
        OperationCanceledException caught = null;
        try { await pending; }
        catch (OperationCanceledException ex) { caught = ex; }

        Assert.NotNull(caught);
        Assert.AreEqual(0, inputs.GetWaitingReadersCount());
    });

    [UnityTest]
    public IEnumerator InputReadStillReturnsPushedValue() => UniTask.ToCoroutine(async () =>
    {
        var inputs = new InputStreamManager();
        var pending = inputs.GetAsync(CancellationToken.None, 60000).Preserve();
        inputs.PushInput(42);

        Assert.AreEqual(42, await pending);
        Assert.AreEqual(0, inputs.GetWaitingReadersCount());
    });

    [UnityTest]
    public IEnumerator GetFailsWhenTotalWaitBudgetRunsOut() => UniTask.ToCoroutine(async () =>
    {
        var program = new RiveParser(_logger).Parse("get_budget", "int g = get\n>> 1");
        var run = new RiveRun { WaitedMilliseconds = RiveExecutor.MaxTotalWaitMs - 50 };

        var result = await NewExecutor().ExecuteAsync(program, Array.Empty<int>(), run);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("Total wait budget", result.ErrorMessage);
        Assert.GreaterOrEqual(run.WaitedMilliseconds, RiveExecutor.MaxTotalWaitMs - 50);
    });

    [UnityTest]
    public IEnumerator GetWithNoBudgetLeftFailsWithoutWaiting() => UniTask.ToCoroutine(async () =>
    {
        var program = new RiveParser(_logger).Parse("get_spent", "int g = get\n>> 1");
        var inputs = new InputStreamManager();
        var run = new RiveRun { WaitedMilliseconds = RiveExecutor.MaxTotalWaitMs };

        var result = await NewExecutor(inputs).ExecuteAsync(program, Array.Empty<int>(), run);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("Total wait budget", result.ErrorMessage);
        Assert.AreEqual(0, inputs.GetWaitingReadersCount());
    });

    [UnityTest]
    public IEnumerator WaitFailsWhenTotalWaitBudgetIsExceeded() => UniTask.ToCoroutine(async () =>
    {
        var program = new RiveParser(_logger).Parse("wait_budget", "wait 100\n>> 1");
        var run = new RiveRun { WaitedMilliseconds = RiveExecutor.MaxTotalWaitMs - 10 };

        var result = await NewExecutor().ExecuteAsync(program, Array.Empty<int>(), run);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("Total wait budget", result.ErrorMessage);
    });

    [UnityTest]
    public IEnumerator WaitsAreAccumulatedAcrossStatements() => UniTask.ToCoroutine(async () =>
    {
        var program = new RiveParser(_logger).Parse("wait_sum", "wait 5\nwait 7\n>> 1");
        var run = new RiveRun();

        var result = await NewExecutor().ExecuteAsync(program, Array.Empty<int>(), run);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(12, run.WaitedMilliseconds);
    });

    [UnityTest]
    public IEnumerator CancelAllTerminatesScriptInsideWait() => UniTask.ToCoroutine(async () =>
    {
        Load("sleeper", "wait 60000\n>> 1");
        var pending = _runtime.ExecuteScriptAsync("sleeper").Preserve();

        _runtime.CancelAll();
        var result = await pending;

        Assert.IsFalse(result.Success);
        StringAssert.Contains("cancelled", result.ErrorMessage);
    });

    [UnityTest]
    public IEnumerator CancelAllTerminatesScriptInsideGet() => UniTask.ToCoroutine(async () =>
    {
        Load("reader_wait", "int g = get\n>> 1");
        var pending = _runtime.ExecuteScriptAsync("reader_wait").Preserve();
        Assert.AreEqual(1, _runtime.GetWaitingInputReaders());

        _runtime.CancelAll();
        var result = await pending;

        Assert.IsFalse(result.Success);
        StringAssert.Contains("cancelled", result.ErrorMessage);
        Assert.AreEqual(0, _runtime.GetWaitingInputReaders());
    });

    [UnityTest]
    public IEnumerator CancelAllTerminatesBusyLoop() => UniTask.ToCoroutine(async () =>
    {
        Load("busy", "for i = 0 to 5000\nint t = i\nend\n>> 1");
        var pending = _runtime.ExecuteScriptAsync("busy").Preserve();

        _runtime.CancelAll();
        var result = await pending;

        Assert.IsFalse(result.Success);
        StringAssert.Contains("cancelled", result.ErrorMessage);
    });

    [UnityTest]
    public IEnumerator CancelAllStopsCallerWhenCalleeIsCancelled() => UniTask.ToCoroutine(async () =>
    {
        Load("inner_wait", "int g = get\n>> 1");
        Load("outer_wait", "int r = inner_wait()\nint after = 5\n>> after");
        var pending = _runtime.ExecuteScriptAsync("outer_wait").Preserve();
        Assert.AreEqual(1, _runtime.GetWaitingInputReaders());

        _runtime.CancelAll();
        var result = await pending;

        Assert.IsFalse(result.Success);
        StringAssert.Contains("cancelled", result.ErrorMessage);
    });

    [UnityTest]
    public IEnumerator RuntimeRunsScriptsAfterCancelAll() => UniTask.ToCoroutine(async () =>
    {
        Load("after_cancel", ">> 9");
        _runtime.CancelAll();

        var result = await _runtime.ExecuteScriptAsync("after_cancel");

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(9, result.ReturnValue);
    });

    [UnityTest]
    public IEnumerator DisposedRuntimeRefusesScripts() => UniTask.ToCoroutine(async () =>
    {
        Load("after_dispose", ">> 9");
        _runtime.Dispose();

        var result = await _runtime.ExecuteScriptAsync("after_dispose");

        Assert.IsFalse(result.Success);
    });

    [UnityTest]
    public IEnumerator ConcurrentRunsAreCapped() => UniTask.ToCoroutine(async () =>
    {
        Load("parked", "int g = get\n>> 1");
        var parked = new UniTask<RiveRuntime.ScriptResult>[RiveRuntime.MaxConcurrentRuns];
        for (int i = 0; i < parked.Length; i++)
            parked[i] = _runtime.ExecuteScriptAsync("parked").Preserve();
        Assert.AreEqual(RiveRuntime.MaxConcurrentRuns, _runtime.GetWaitingInputReaders());

        var refused = await _runtime.ExecuteScriptAsync("parked");
        Assert.IsFalse(refused.Success);
        StringAssert.Contains("Too many running scripts", refused.ErrorMessage);

        _runtime.PushInput(0);
        foreach (var task in parked)
            Assert.IsTrue((await task).Success);

        _runtime.PushInput(0);
        var again = _runtime.ExecuteScriptAsync("parked").Preserve();
        _runtime.PushInput(0);
        Assert.IsTrue((await again).Success);
    });
}
