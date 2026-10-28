using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public unsafe struct ChunkTickSource
    {
        [NativeDisableUnsafePtrRestriction] public int* Occupancy;
        [NativeDisableUnsafePtrRestriction] public ObjectInstInfo* Objects;
        public int Count;
        public int Ring;
        public int Rounds;
        public long Key;

        public static ChunkTickSource Create(NativeArray<int> occupancy, NativeList<ObjectInstInfo> objects, long key, int ring, int rounds) =>
            new ChunkTickSource
            {
                Occupancy = (int*)occupancy.GetUnsafeReadOnlyPtr(),
                Objects = (ObjectInstInfo*)objects.GetUnsafeReadOnlyPtr(),
                Count = objects.Length,
                Ring = ring,
                Rounds = rounds,
                Key = key
            };
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
    public struct RandomTickJob : IJobFor
    {
        [ReadOnly] public NativeArray<ChunkTickSource> Sources;
        [ReadOnly] public NativeArray<byte> AcceptById;
        [NativeDisableParallelForRestriction] public NativeArray<RandomTickEvent> Output;
        [NativeDisableParallelForRestriction] public NativeArray<int> Counter;
        public float CellsPerChunkPerTick;
        public float FarCellDivisor;
        public int TotalCells;
        public uint Seed;

        public unsafe void Execute(int index)
        {
            ChunkTickSource s = Sources[index];
            var rng = Random.CreateFromIndex(Seed + (uint)index * 747796405u);

            float cells = (s.Ring == 0 ? CellsPerChunkPerTick : CellsPerChunkPerTick / (FarCellDivisor * s.Ring)) * s.Rounds;
            int n = (int)cells;
            if (rng.NextFloat() < cells - n) n++;

            int* counter = (int*)Counter.GetUnsafePtr();
            for (int k = 0; k < n; k++)
            {
                int cell = rng.NextInt(TotalCells);
                int handle = s.Occupancy[cell];
                if (handle <= 0 || handle > s.Count) continue;

                ObjectInstInfo obj = s.Objects[handle - 1];
                if ((uint)obj.PrefabID >= (uint)AcceptById.Length || AcceptById[obj.PrefabID] == 0) continue;

                int slot = Interlocked.Increment(ref counter[0]) - 1;
                if (slot >= Output.Length) continue;

                Output[slot] = new RandomTickEvent
                {
                    Chunk = s.Key,
                    Cell = cell,
                    PrefabId = obj.PrefabID,
                    Position = obj.Position,
                    Ring = (byte)s.Ring
                };
            }
        }
    }
}
