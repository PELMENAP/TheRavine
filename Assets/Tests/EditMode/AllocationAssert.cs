using System;
using NUnit.Framework;
using Is = UnityEngine.TestTools.Constraints.Is;

public static class AllocationAssert
{
    public static void Zero(TestDelegate hot, int iterations, string what)
    {
        hot();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) hot();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(0L, bytes, $"{what}: {bytes} байт GC за {iterations} вызовов");
        Assert.That(hot, Is.Not.AllocatingGCMemory(), what);
    }
}
