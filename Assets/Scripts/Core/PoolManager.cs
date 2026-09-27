using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.ObjectControl
{
    public sealed class PooledObject
    {
        public readonly GameObject GameObject;
        public readonly Transform Transform;
        public readonly PoolManager.Pool Owner;
        private readonly IPlaceable placeable;

        public Vector3 Position { get; private set; }
        public bool IsActive { get; internal set; }

        internal PooledObject(GameObject gameObject, PoolManager.Pool owner)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            Owner = owner;
            placeable = gameObject.GetComponent<IPlaceable>();
            gameObject.SetActive(false);
        }

        internal void Place(Vector3 position)
        {
            Position = position;
            Transform.position = position;
            placeable?.Place(SeedOf(position));
        }

        public static int SeedOf(Vector3 position) =>
            (int)(math.hash(new float3(position.x, position.y, position.z)) & 0x7FFFFFFFu);
    }

    public sealed class PoolManager : IDisposable
    {
        public sealed class Pool
        {
            public readonly int Id;
            private readonly GameObject prefab;
            private readonly Transform holder;
            private readonly Stack<PooledObject> inactive;

            public int ActiveCount { get; private set; }
            public int InactiveCount => inactive.Count;

            internal Pool(int id, GameObject prefab, Transform holder, int capacity)
            {
                Id = id;
                this.prefab = prefab;
                this.holder = holder;
                inactive = new Stack<PooledObject>(math.max(capacity, 4));
            }

            public void Prewarm(int count)
            {
                for (int i = inactive.Count; i < count; i++)
                    inactive.Push(Create());
            }

            public PooledObject Get(Vector3 position)
            {
                PooledObject obj = inactive.Count > 0 ? inactive.Pop() : Create();
                obj.Place(position);
                obj.IsActive = true;
                obj.GameObject.SetActive(true);
                ActiveCount++;
                return obj;
            }

            public void Move(PooledObject obj, Vector3 position)
            {
                if (obj == null || !obj.IsActive) return;
                obj.Place(position);
            }

            public void Release(PooledObject obj)
            {
                if (obj == null || !obj.IsActive || obj.Owner != this) return;
                obj.IsActive = false;
                obj.GameObject.SetActive(false);
                inactive.Push(obj);
                ActiveCount--;
            }

            private PooledObject Create() =>
                new(UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, holder), this);
        }

        private readonly Transform parent;
        private readonly Dictionary<int, Pool> pools = new();

        public PoolManager(Transform parent) => this.parent = parent;

        public Pool Register(int id, GameObject prefab, int prewarm = 0)
        {
            if (pools.TryGetValue(id, out Pool pool))
            {
                pool.Prewarm(prewarm);
                return pool;
            }
            if (prefab == null) return null;

            GameObject holder = new(prefab.name + " pool");
            holder.transform.SetParent(parent, false);

            pool = new Pool(id, prefab, holder.transform, prewarm);
            pools.Add(id, pool);
            pool.Prewarm(prewarm);
            return pool;
        }

        public bool TryGetPool(int id, out Pool pool) => pools.TryGetValue(id, out pool);

        public PooledObject Get(int id, Vector3 position) =>
            pools.TryGetValue(id, out Pool pool) ? pool.Get(position) : null;

        public void Release(int id, PooledObject obj)
        {
            if (obj != null && pools.TryGetValue(id, out Pool pool))
                pool.Release(obj);
        }

        public void Dispose() => pools.Clear();
    }
}
