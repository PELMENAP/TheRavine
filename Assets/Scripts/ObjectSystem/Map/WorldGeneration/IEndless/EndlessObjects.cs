using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.ObjectControl;

namespace TheRavine.Generator.EndlessGenerators
{
    public sealed class EndlessObjects : IEndless
    {
        private const int ViewCapacity = MapGenerator.WindowSide * MapGenerator.WindowSide;

        private sealed class ChunkView
        {
            public long Key;
            public int Version;
            public int CellCount;
            public readonly PooledObject[] ByCell = new PooledObject[ChunkData.TotalCells];
            public readonly int[] Stamp = new int[ChunkData.TotalCells];
            public readonly int[] Cells = new int[ChunkData.TotalCells];
        }

        private readonly MapGenerator generator;
        private readonly ObjectInfoRegistry registry;
        private readonly PoolManager.Pool[] poolByDense;
        private readonly bool[] lifecycleByDense;
        private readonly LongDictionary<ChunkView> views = new(ViewCapacity * 2);
        private readonly Stack<ChunkView> freeViews = new(ViewCapacity);
        private readonly long[] staleKeys = new long[ViewCapacity * 2];
        private int stamp;

        public EndlessObjects(MapGenerator generator, ObjectSystem objectSystem)
        {
            this.generator = generator;
            registry = objectSystem.infoRegistry;

            int count = registry.Count;
            poolByDense = new PoolManager.Pool[count];
            lifecycleByDense = new bool[count];

            for (int i = 0; i < count; i++)
            {
                ObjectInfo info = registry.GetDense(i);
                objectSystem.TryGetPool(info.Id, out poolByDense[i]);
                lifecycleByDense[i] = info.HasLifecycle;
            }
        }

        public void UpdateChunk(long center)
        {
            int stale = 0;
            foreach (var kv in views)
            {
                if (!MapGenerator.IsInWindow(center, kv.Key, MapGenerator.chunkScale))
                    staleKeys[stale++] = kv.Key;
            }

            for (int i = 0; i < stale; i++)
            {
                if (!views.TryRemove(staleKeys[i], out ChunkView view)) continue;
                ReleaseView(view);
                freeViews.Push(view);
            }

            for (int gz = 0; gz < MapGenerator.WindowSide; gz++)
            for (int gx = 0; gx < MapGenerator.WindowSide; gx++)
            {
                long key = MapGenerator.WindowChunk(center, gx, gz);
                ChunkData cd = generator.GetMapData(key);

                if (views.TryGetValue(key, out ChunkView view))
                {
                    if (view.Version != cd.Version) Refresh(view, cd);
                    continue;
                }

                view = freeViews.Count > 0 ? freeViews.Pop() : new ChunkView();
                view.Key = key;
                view.CellCount = 0;
                views[key] = view;

                Refresh(view, cd);
                RegisterLifecycle(cd);
            }
        }

        public void OnChunkDirty(long chunkKey)
        {
            if (!views.TryGetValue(chunkKey, out ChunkView view)) return;
            if (!generator.TryGetChunk(chunkKey, out ChunkData cd)) return;
            if (view.Version != cd.Version) Refresh(view, cd);
        }

        public void Tick(long deadline) { }

        private void Refresh(ChunkView view, ChunkData cd)
        {
            int s = ++stamp;

            for (int i = 0; i < cd.Objects.Length; i++)
            {
                ObjectInstInfo info = cd.Objects[i];
                int dense = registry.DenseIndexOf(info.PrefabID);
                if (dense < 0) continue;

                PoolManager.Pool pool = poolByDense[dense];
                int cell = info.PrimaryIdx;
                if (pool == null || (uint)cell >= ChunkData.TotalCells) continue;

                PooledObject current = view.ByCell[cell];
                if (current != null)
                {
                    if (current.Owner == pool)
                    {
                        if (current.Position != info.Position) pool.Move(current, info.Position);
                        view.Stamp[cell] = s;
                        continue;
                    }
                    current.Owner.Release(current);
                }
                else
                {
                    view.Cells[view.CellCount++] = cell;
                }

                view.ByCell[cell] = pool.Get(info.Position);
                view.Stamp[cell] = s;
            }

            int write = 0;
            for (int k = 0; k < view.CellCount; k++)
            {
                int cell = view.Cells[k];
                if (view.Stamp[cell] == s)
                {
                    view.Cells[write++] = cell;
                    continue;
                }

                PooledObject obj = view.ByCell[cell];
                obj?.Owner.Release(obj);
                view.ByCell[cell] = null;
            }

            view.CellCount = write;
            view.Version = cd.Version;
        }

        private void RegisterLifecycle(ChunkData cd)
        {
            for (int i = 0; i < cd.Objects.Length; i++)
            {
                ObjectInstInfo info = cd.Objects[i];
                int dense = registry.DenseIndexOf(info.PrefabID);
                if (dense < 0 || !lifecycleByDense[dense]) continue;

                generator.AddNALObject(new Vector2Int(
                    (int)math.floor(info.Position.x),
                    (int)math.floor(info.Position.z)));
            }
        }

        private static void ReleaseView(ChunkView view)
        {
            for (int k = 0; k < view.CellCount; k++)
            {
                int cell = view.Cells[k];
                PooledObject obj = view.ByCell[cell];
                obj?.Owner.Release(obj);
                view.ByCell[cell] = null;
            }

            view.CellCount = 0;
        }

        public void Dispose()
        {
            views.Clear();
            freeViews.Clear();
        }
    }
}
