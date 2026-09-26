using System;
using Unity.Collections;
using Unity.Mathematics;

public sealed class CasteThresholdSystem : IDisposable
{
    private NativeArray<float4> _thresholds;

    public int Capacity => _thresholds.IsCreated ? _thresholds.Length : 0;

    public CasteThresholdSystem(int capacity)
        => _thresholds = new NativeArray<float4>(Math.Max(capacity, 1), Allocator.Persistent);

    public static float4 Initial()
    {
        ref readonly var r = ref SimulationRules.Frame;
        return new float4(r.CasteWorkerThreshold, r.CasteSoldierThreshold, r.CasteScoutThreshold, r.CasteNurseThreshold);
    }

    public void Reset(int index, float4 value)
    {
        if ((uint)index < (uint)Capacity) _thresholds[index] = value;
    }

    public float4 Get(int index) => (uint)index < (uint)Capacity ? _thresholds[index] : Initial();

    public void Reinforce(int index, Caste caste, bool success)
    {
        if ((uint)index >= (uint)Capacity || caste >= Caste.Count) return;
        ref readonly var r = ref SimulationRules.Frame;
        float4 t = _thresholds[index];
        int c = (int)caste;
        t[c] = math.clamp(t[c] + (success ? -r.CasteThresholdLearn : r.CasteThresholdForget),
            r.CasteThresholdMin, r.CasteThresholdMax);
        _thresholds[index] = t;
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)Capacity || (uint)last >= (uint)Capacity) return;
        if (index != last) _thresholds[index] = _thresholds[last];
        _thresholds[last] = default;
    }

    public void Dispose()
    {
        if (_thresholds.IsCreated) _thresholds.Dispose();
    }
}
