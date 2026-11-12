using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.ObjectControl;

namespace TheRavine.Generator.EndlessGenerators
{
    public sealed class EndlessObjects : IEndless, IObjectViewMotionHost
    {
        private const int ViewCapacity = MapGenerator.WindowSide * MapGenerator.WindowSide;
        private const int LevelShift = 3;
        private const int SlotCount = ChunkData.TotalCells << LevelShift;

        private sealed class ChunkView
        {
            public long Key;
            public int Version;
            public int CellCount;
            public readonly PooledObject[] ByCell = new PooledObject[SlotCount];
            public readonly int[] Stamp = new int[SlotCount];
            public readonly int[] Cells = new int[SlotCount];
        }

        private readonly MapGenerator generator;
        private readonly ObjectInfoRegistry registry;
        private readonly PoolManager.Pool[] poolByDense;
        private readonly LongDictionary<ChunkView> views = new(ViewCapacity * 2);
        private readonly Stack<ChunkView> freeViews = new(ViewCapacity);
        private readonly long[] staleKeys = new long[ViewCapacity * 2];
        private readonly ObjectViewMotion motion;
        private readonly Dictionary<long, Vector3> growthRatios = new(16);
        private readonly List<Renderer> measured = new(8);
        private int stamp;

        public EndlessObjects(MapGenerator generator, ObjectSystem objectSystem, ObjectViewMotionSettings motionSettings = null)
        {
            this.generator = generator;
            registry = objectSystem.infoRegistry;
            motion = new ObjectViewMotion(motionSettings, this);

            int count = registry.Count;
            poolByDense = new PoolManager.Pool[count];

            for (int i = 0; i < count; i++)
            {
                ObjectInfo info = registry.GetDense(i);
                objectSystem.TryGetPool(info.Id, out poolByDense[i]);
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
                    if (view.Version != cd.Version) Refresh(view, cd, true);
                    continue;
                }

                view = freeViews.Count > 0 ? freeViews.Pop() : new ChunkView();
                view.Key = key;
                view.CellCount = 0;
                views[key] = view;

                Refresh(view, cd, false);
            }
        }

        public void OnChunkDirty(long chunkKey)
        {
            if (!views.TryGetValue(chunkKey, out ChunkView view)) return;
            if (!generator.TryGetChunk(chunkKey, out ChunkData cd)) return;
            if (view.Version != cd.Version) Refresh(view, cd, true);
        }

        public void Tick(long deadline) { }

        public void OnGrowthFinished(long viewKey, int cell, PooledObject grown, PoolManager.Pool next, Vector3 nextPosition)
        {
            grown.Owner.Release(grown);
            if (!views.TryGetValue(viewKey, out ChunkView view) || view.ByCell[cell] != grown) return;
            view.ByCell[cell] = next.Get(nextPosition);
        }

        private void Refresh(ChunkView view, ChunkData cd, bool animate)
        {
            int s = ++stamp;

            for (int i = 0; i < cd.Objects.Length; i++)
            {
                ObjectInstInfo info = cd.Objects[i];
                int dense = registry.DenseIndexOf(info.PrefabID);
                if (dense < 0) continue;

                PoolManager.Pool pool = poolByDense[dense];
                if (pool == null || (uint)info.PrimaryIdx >= ChunkData.TotalCells || info.BaseLevel >= ObjectInfo.MaxLevels) continue;

                int cell = (info.PrimaryIdx << LevelShift) | info.BaseLevel;
                Vector3 position = info.Position;

                PooledObject current = view.ByCell[cell];
                if (current != null)
                {
                    if (current.Owner == pool)
                    {
                        if (motion.TryGetSlot(current, out ObjectViewMotion.Slot running) && running.Kind == ObjectViewMotion.Kind.Grow)
                            motion.Cancel(current);
                        if (current.Position != position) pool.Move(current, position);
                        view.Stamp[cell] = s;
                        continue;
                    }

                    if (animate && TryGrow(view, cell, current, pool, info.PrefabID, position))
                    {
                        view.Stamp[cell] = s;
                        continue;
                    }
                    Dismiss(view, current, animate);
                }
                else
                {
                    view.Cells[view.CellCount++] = cell;
                }

                PooledObject created = pool.Get(position);
                view.ByCell[cell] = created;
                view.Stamp[cell] = s;
                if (animate) motion.PopIn(created, view.Key);
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
                if (obj != null) Dismiss(view, obj, animate);
                view.ByCell[cell] = null;
            }

            view.CellCount = write;
            view.Version = cd.Version;
        }

        private void Dismiss(ChunkView view, PooledObject obj, bool animate)
        {
            if (animate && motion.ShrinkOut(obj, view.Key)) return;
            motion.Cancel(obj);
            obj.Owner.Release(obj);
        }

        private bool TryGrow(ChunkView view, int cell, PooledObject current, PoolManager.Pool next, int nextId, Vector3 position)
        {
            if (motion.TryGetSlot(current, out ObjectViewMotion.Slot running)
                && running.Kind == ObjectViewMotion.Kind.Grow
                && running.NextPool == next)
            {
                running.NextPosition = position;
                return true;
            }

            ObjectInfo from = registry.Get(current.Owner.Id);
            ObjectInfo to = registry.Get(nextId);
            if (from == null || to == null || from.BehaviourType != BehaviourType.GROW || !ReferenceEquals(from.EvolutionStep, to))
                return false;

            return motion.Grow(current, view.Key, cell, next, position, GrowthRatioOf(current, next, from, to));
        }

        private Vector3 GrowthRatioOf(PooledObject from, PoolManager.Pool next, ObjectInfo infoFrom, ObjectInfo infoNext)
        {
            long key = ((long)from.Owner.Id << 32) | (uint)next.Id;
            if (growthRatios.TryGetValue(key, out Vector3 cached)) return cached;

            Vector3 fromSize = default, toSize = default;
            bool known = TryMeasure(from, out fromSize)
                && next.TryGetFirstActive(out PooledObject sample)
                && TryMeasure(sample, out toSize);

            Vector3 ratio = ObjectGrowthMath.Resolve(fromSize, toSize, CollisionHeight(infoFrom.Id), CollisionHeight(infoNext.Id));
            if (known) growthRatios[key] = ratio;
            return ratio;
        }

        private float CollisionHeight(int id)
        {
            CollisionTable table = generator.Collision;
            if (table == null) return 0f;

            NativeArray<CollisionShape> shapes = table.Set.Shapes;
            return shapes.IsCreated && (uint)id < (uint)shapes.Length ? shapes[id].Height : 0f;
        }

        private bool TryMeasure(PooledObject obj, out Vector3 size)
        {
            size = default;
            measured.Clear();
            obj.GameObject.GetComponentsInChildren(false, measured);

            bool any = false;
            Bounds total = default;
            for (int i = 0; i < measured.Count; i++)
            {
                Renderer renderer = measured[i];
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SpriteRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (any) total.Encapsulate(renderer.bounds);
                else total = renderer.bounds;
                any = true;
            }
            measured.Clear();

            if (!any) return false;
            size = ObjectGrowthMath.SizeAtBase(total.size, obj.Transform.localScale, obj.BaseScale);
            return true;
        }

        private void ReleaseView(ChunkView view)
        {
            motion.CancelView(view.Key);
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
            motion.CancelAll();
            views.Clear();
            freeViews.Clear();
        }
    }
}
