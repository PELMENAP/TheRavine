using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct SkillDecayJob : IJobParallelFor
{
    public int Offset;
    public int Count;
    public int SkillCount;
    public float Now;
    public float DecayRate;
    public float SavingsFloor;

    [NativeDisableParallelForRestriction] public NativeArray<float> Levels;
    [NativeDisableParallelForRestriction] public NativeArray<float> LastUsed;
    [NativeDisableParallelForRestriction] public NativeArray<byte> Dirty;
    [ReadOnly] public NativeArray<float> Peaks;
    [ReadOnly] public NativeArray<byte> Colonies;
    [ReadOnly] public NativeArray<float> ColonyCycle;
    [ReadOnly] public NativeArray<ulong> ColonyPruned;

    public void Execute(int i)
    {
        int index = Offset + i;
        if (index >= Count) index -= Count;
        int colony = Colonies[index];
        ulong pruned = ColonyPruned[colony];
        int baseIndex = index * SkillCount;
        int cycleBase = colony * SkillCount;
        byte changed = 0;
        for (int v = 0; v < SkillCount; v++)
        {
            if (((pruned >> v) & 1ul) != 0ul) continue;
            int slot = baseIndex + v;
            float before = Levels[slot];
            float after = SkillGraphMath.Decay(before, Peaks[slot], Now - LastUsed[slot], ColonyCycle[cycleBase + v], DecayRate, SavingsFloor);
            changed |= math.floor(after) != math.floor(before) ? (byte)1 : (byte)0;
            Levels[slot] = after;
            LastUsed[slot] = Now;
        }
        if (changed != 0) Dirty[index] = 1;
    }
}
