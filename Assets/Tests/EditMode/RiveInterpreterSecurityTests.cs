using System;
using System.Collections;
using System.Diagnostics;
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
        _logger = new RavineLogger(null);
        _runtime = new RiveRuntime(null, _logger);
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
}
