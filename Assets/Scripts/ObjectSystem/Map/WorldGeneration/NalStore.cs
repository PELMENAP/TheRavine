using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public sealed class NalStore
    {
        public NativeList<int2> Cell;
        public NativeList<int> Dense;
        public NativeList<int> PrefabId;
        public NativeList<double> Due;
        public NativeHashMap<long, int> SlotByPos;

        public NalStore(int capacity)
        {
            capacity = math.max(capacity, 4);
            Cell = new NativeList<int2>(capacity, Allocator.Persistent);
            Dense = new NativeList<int>(capacity, Allocator.Persistent);
            PrefabId = new NativeList<int>(capacity, Allocator.Persistent);
            Due = new NativeList<double>(capacity, Allocator.Persistent);
            SlotByPos = new NativeHashMap<long, int>(capacity, Allocator.Persistent);
        }

        public int Count => Cell.Length;
        public bool IsCreated => Cell.IsCreated;

        public bool Add(Vector2Int pos, int prefabId, int dense, double due)
        {
            if (!SlotByPos.TryAdd(Position2Int.Pack(pos), Cell.Length)) return false;

            Cell.Add(new int2(pos.x, pos.y));
            Dense.Add(dense);
            PrefabId.Add(prefabId);
            Due.Add(due);

            Debug.Assert(SlotByPos.Count == Cell.Length, "NalStore: map/slots desync");
            return true;
        }

        public bool Remove(long pos)
        {
            if (!SlotByPos.TryGetValue(pos, out int slot)) return false;

            SlotByPos.Remove(pos);
            Cell.RemoveAtSwapBack(slot);
            Dense.RemoveAtSwapBack(slot);
            PrefabId.RemoveAtSwapBack(slot);
            Due.RemoveAtSwapBack(slot);

            if (slot < Cell.Length)
            {
                int2 moved = Cell[slot];
                SlotByPos[Position2Int.Pack(moved.x, moved.y)] = slot;
            }

            Debug.Assert(SlotByPos.Count == Cell.Length, "NalStore: map/slots desync");
            return true;
        }

        public bool Contains(long pos) => SlotByPos.ContainsKey(pos);

        public bool TryGetSlot(long pos, out int slot) => SlotByPos.TryGetValue(pos, out slot);

        public void Clear()
        {
            Cell.Clear();
            Dense.Clear();
            PrefabId.Clear();
            Due.Clear();
            SlotByPos.Clear();
        }

        public bool IsConsistent()
        {
            if (SlotByPos.Count != Cell.Length) return false;
            if (Dense.Length != Cell.Length || PrefabId.Length != Cell.Length || Due.Length != Cell.Length) return false;

            for (int i = 0; i < Cell.Length; i++)
            {
                int2 c = Cell[i];
                if (!SlotByPos.TryGetValue(Position2Int.Pack(c.x, c.y), out int slot) || slot != i) return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (Cell.IsCreated) Cell.Dispose();
            if (Dense.IsCreated) Dense.Dispose();
            if (PrefabId.IsCreated) PrefabId.Dispose();
            if (Due.IsCreated) Due.Dispose();
            if (SlotByPos.IsCreated) SlotByPos.Dispose();
        }
    }
}
