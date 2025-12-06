using System.Collections.Generic;
using UnityEngine;

namespace TheRavine.ObjectControl
{
    public delegate GameObject CreateInstance(Vector3 position, GameObject prefab);

    public class PoolManager
    {
        private readonly Transform parent;
        public PoolManager(Transform _parent) => parent = _parent;

        private class PoolData
        {
            public Queue<ObjectInstance> Objects { get; } = new();
            public Transform Parent { get; }
            public int Size { get; set; }

            public PoolData(Transform parent, int size)
            {
                Parent = parent;
                Size = size;
            }
        }

        private readonly Dictionary<int, PoolData> pools = new();

        public void CreatePool(int poolKey, GameObject prefab, CreateInstance createInstance, int poolSize = 1)
        {
            if(prefab == null) return;
            if (!pools.ContainsKey(poolKey))
            {
                GameObject poolHolder = new(prefab.name + " pool") { isStatic = true };
                poolHolder.transform.parent = parent;
                pools[poolKey] = new PoolData(poolHolder.transform, poolSize);
            }

            var pool = pools[poolKey];
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = createInstance?.Invoke(Vector3.zero, prefab);
                pool.Objects.Enqueue(new ObjectInstance(obj, pool.Parent));
            }
        }

        public void Reuse(int prefabID, Vector3 position)
        {
            if (!pools.ContainsKey(prefabID)) return;

            var pool = pools[prefabID];
            if (pool.Objects.Count == 0) return;

            ObjectInstance instance = pool.Objects.Dequeue();
            instance.Reuse(position);
            pool.Objects.Enqueue(instance);
        }

        public void Deactivate(int prefabID)
        {
            if (!pools.ContainsKey(prefabID)) return;

            var pool = pools[prefabID];
            if (pool.Objects.Count == 0) return;

            ObjectInstance instance = pool.Objects.Dequeue();
            instance.Deactivate();
            pool.Objects.Enqueue(instance);
        }

        public int GetPoolSize(int prefabID) => pools.ContainsKey(prefabID) ? pools[prefabID].Size : (int)0;
        public void IncreasePoolSize(int prefabID) { if (pools.ContainsKey(prefabID)) pools[prefabID].Size++; }

        private class ObjectInstance
        {
            private readonly GameObject gameObject;
            private readonly Transform transform;

            public ObjectInstance(GameObject obj, Transform parent)
            {
                gameObject = obj;
                transform = obj.transform;
                obj.transform.parent = parent;
                obj.SetActive(false);
            }

            public void Reuse(Vector3 position)
            {
                transform.position = position;
                gameObject.SetActive(true);
            }

            public void Deactivate()
            {
                gameObject.SetActive(false);
            }
        }
    }
}