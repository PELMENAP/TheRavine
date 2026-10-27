using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public struct NalDecision
    {
        public const int MaxSpawns = 4;

        public int Slot;
        public bool DeathRoll;
        public int SpawnCount;
        public int SpawnDense;
        public int4 TargetsX;
        public int4 TargetsZ;
        public double NextDue;
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    public struct NalDecideJob : IJobFor
    {
        [ReadOnly] public NativeArray<int2> Cell;
        [ReadOnly] public NativeArray<int> Dense;
        [ReadOnly] public NativeArray<double> Due;
        [ReadOnly] public NativeArray<NalSpeciesParams> Species;
        [NativeDisableParallelForRestriction] public NativeArray<NalDecision> Output;
        [NativeDisableParallelForRestriction] public NativeArray<int> Counter;
        public double Now;
        public float DeadChance;
        public uint Seed;
        public float SecondsPerDelayUnit;
        public float FallbackDelaySeconds;
        public int GrowDecayChance;
        public int CatchUpMaxSteps;

        public unsafe void Execute(int slot)
        {
            if (Due[slot] > Now) return;

            int index = Interlocked.Increment(ref ((int*)Counter.GetUnsafePtr())[0]) - 1;
            if (index >= Output.Length) return;

            NalSpeciesParams p = Species[Dense[slot]];
            var rng = Unity.Mathematics.Random.CreateFromIndex(Seed + (uint)slot * 747796405u);

            double period = p.Period(SecondsPerDelayUnit, FallbackDelaySeconds);
            var d = new NalDecision
            {
                Slot = slot,
                SpawnDense = p.EvolutionDense,
                NextDue = math.max(Due[slot], Now - period * math.max(CatchUpMaxSteps - 1, 0)) + period
            };

            if (p.Kind == (byte)BehaviourType.GROW)
            {
                if (p.EvolutionDense < 0)
                    d.DeathRoll = rng.NextInt(1, 101) <= GrowDecayChance;
            }
            else if (p.Kind == (byte)BehaviourType.NAL)
            {
                d.DeathRoll = rng.NextInt(1, 101) <= (p.Chance >> 1) + DeadChance;

                if (p.EvolutionDense >= 0)
                {
                    int2 origin = Cell[slot];
                    int attempts = p.Attempt;
                    for (int a = 0; a < attempts && d.SpawnCount < NalDecision.MaxSpawns; a++)
                    {
                        if (rng.NextInt(1, 101) > p.Chance) continue;

                        int2 target = origin + (int2)(rng.NextFloat2(-1f, 1f) * p.Distance);
                        d.TargetsX[d.SpawnCount] = target.x;
                        d.TargetsZ[d.SpawnCount] = target.y;
                        d.SpawnCount++;
                    }
                }
            }

            Output[index] = d;
        }
    }
}
