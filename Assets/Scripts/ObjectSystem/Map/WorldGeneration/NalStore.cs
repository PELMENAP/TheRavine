using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public sealed class NalStore
    {
        public NativeList<int2> Cell;
        public NativeList<long> Chunk;
        public NativeList<int> Dense;
        public NativeList<int> PrefabId;
        public NativeList<double> Due;
        public NativeHashMap<long, int> SlotByPos;
        public NativeHashMap<long, double> LeftAt;

        public NalStore(int capacity)
        {
            capacity = math.max(capacity, 4);
            Cell = new NativeList<int2>(capacity, Allocator.Persistent);
            Chunk = new NativeList<long>(capacity, Allocator.Persistent);
            Dense = new NativeList<int>(capacity, Allocator.Persistent);
            PrefabId = new NativeList<int>(capacity, Allocator.Persistent);
            Due = new NativeList<double>(capacity, Allocator.Persistent);
            SlotByPos = new NativeHashMap<long, int>(capacity, Allocator.Persistent);
            LeftAt = new NativeHashMap<long, double>(64, Allocator.Persistent);
        }

        public int Count => Cell.Length;
        public bool IsCreated => Cell.IsCreated;

        public bool Add(Vector2Int pos, int prefabId, int dense, double due)
        {
            long packed = Position2Int.Pack(pos);
            if (!SlotByPos.TryAdd(packed, Cell.Length)) return false;

            Cell.Add(new int2(pos.x, pos.y));
            Chunk.Add(MapGenerator.WorldToChunk(packed));
            Dense.Add(dense);
            PrefabId.Add(prefabId);
            Due.Add(due);

            Debug.Assert(SlotByPos.Count == Cell.Length, "NalStore: map/slots desync");
            return true;
        }

        public bool Remove(long pos)
        {
            if (!SlotByPos.TryGetValue(pos, out int slot)) return false;

            RemoveSlot(slot);
            return true;
        }

        public int RemoveChunk(long chunk)
        {
            int removed = 0;
            for (int i = Chunk.Length - 1; i >= 0; i--)
            {
                if (Chunk[i] != chunk) continue;

                RemoveSlot(i);
                removed++;
            }

            return removed;
        }

        private void RemoveSlot(int slot)
        {
            int2 cell = Cell[slot];
            SlotByPos.Remove(Position2Int.Pack(cell.x, cell.y));
            Cell.RemoveAtSwapBack(slot);
            Chunk.RemoveAtSwapBack(slot);
            Dense.RemoveAtSwapBack(slot);
            PrefabId.RemoveAtSwapBack(slot);
            Due.RemoveAtSwapBack(slot);

            if (slot < Cell.Length)
            {
                int2 moved = Cell[slot];
                SlotByPos[Position2Int.Pack(moved.x, moved.y)] = slot;
            }

            Debug.Assert(SlotByPos.Count == Cell.Length, "NalStore: map/slots desync");
        }

        public bool Contains(long pos) => SlotByPos.ContainsKey(pos);

        public bool TryGetSlot(long pos, out int slot) => SlotByPos.TryGetValue(pos, out slot);

        public void SetLeftAt(long chunk, double time) => LeftAt[chunk] = time;

        public bool TakeLeftAt(long chunk, out double time)
        {
            if (!LeftAt.TryGetValue(chunk, out time)) return false;

            LeftAt.Remove(chunk);
            return true;
        }

        public void Clear()
        {
            Cell.Clear();
            Chunk.Clear();
            Dense.Clear();
            PrefabId.Clear();
            Due.Clear();
            SlotByPos.Clear();
            LeftAt.Clear();
        }

        public bool IsConsistent()
        {
            if (SlotByPos.Count != Cell.Length) return false;
            if (Chunk.Length != Cell.Length || Dense.Length != Cell.Length
                || PrefabId.Length != Cell.Length || Due.Length != Cell.Length) return false;

            for (int i = 0; i < Cell.Length; i++)
            {
                int2 c = Cell[i];
                long packed = Position2Int.Pack(c.x, c.y);
                if (!SlotByPos.TryGetValue(packed, out int slot) || slot != i) return false;
                if (Chunk[i] != MapGenerator.WorldToChunk(packed)) return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (Cell.IsCreated) Cell.Dispose();
            if (Chunk.IsCreated) Chunk.Dispose();
            if (Dense.IsCreated) Dense.Dispose();
            if (PrefabId.IsCreated) PrefabId.Dispose();
            if (Due.IsCreated) Due.Dispose();
            if (SlotByPos.IsCreated) SlotByPos.Dispose();
            if (LeftAt.IsCreated) LeftAt.Dispose();
        }
    }
}
