using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public static class MimicLegSystem
{
    private static Mimic[] _queue = new Mimic[64];
    private static int _queueCount;
    private static NativeArray<MimicWork> _work;
    private static MimicLegDriver _driver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        System.Array.Clear(_queue, 0, _queue.Length);
        _queueCount = 0;
        if (_work.IsCreated) _work.Dispose();
        _driver = null;
    }

    public static void Enqueue(Mimic mimic)
    {
        if (_driver == null)
        {
            var go = new GameObject("MimicLegSystem") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<MimicLegDriver>();
        }

        if (_queueCount == _queue.Length) System.Array.Resize(ref _queue, _queue.Length << 1);
        _queue[_queueCount++] = mimic;
    }

    public static unsafe void Run()
    {
        if (_queueCount == 0) return;

        int count = 0;
        for (int i = 0; i < _queueCount; i++)
        {
            var mimic = _queue[i];
            _queue[i] = null;
            if (mimic == null || !mimic.Queued) continue;
            _queue[count++] = mimic;
        }
        _queueCount = 0;
        if (count == 0) return;

        if (!_work.IsCreated || _work.Length < count)
        {
            if (_work.IsCreated) _work.Dispose();
            _work = new NativeArray<MimicWork>(Unity.Mathematics.math.ceilpow2(count), Allocator.Persistent);
        }

        for (int i = 0; i < count; i++) _work[i] = _queue[i].BuildWork();

        new MimicLegJob { Work = _work }.Schedule(count, 1).Complete();

        for (int i = 0; i < count; i++)
        {
            var mimic = _queue[i];
            _queue[i] = null;
            mimic.ApplyWork(_work[i]);
        }
    }
}
