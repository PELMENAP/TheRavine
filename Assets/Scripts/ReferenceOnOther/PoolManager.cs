using System.Collections.Generic;
using UnityEngine;
using TheRavine.Extensions;

namespace TheRavine.ObjectControl
{
    public delegate GameObject CreateInstance(Vector3 position, GameObject prefab);

    public class PoolManager
    {
        private Transform parent;
        public PoolManager(Transform _parent) => parent = _parent;

        private class PoolData
        {
            public Queue<ObjectInstance> Objects { get; } = new();
            public Transform Parent { get; }
            public ushort Size { get; set; }

            public PoolData(Transform parent, ushort size)
            {
                Parent = parent;
                Size = size;
            }
        }

        private readonly Dictionary<int, PoolData> pools = new();

        public void CreatePool(int poolKey, GameObject prefab, CreateInstance createInstance, ushort poolSize = 1)
        {
            if(prefab == null) return;
            if (!pools.ContainsKey(poolKey))
            {
                GameObject poolHolder = new GameObject(prefab.name + " pool") { isStatic = true };
                poolHolder.transform.parent = parent;
                pools[poolKey] = new PoolData(poolHolder.transform, poolSize);
            }

            var pool = pools[poolKey];
            for (ushort i = 0; i < poolSize; i++)
            {
                GameObject obj = createInstance?.Invoke(Vector3.zero, prefab);
                pool.Objects.Enqueue(new ObjectInstance(obj, pool.Parent));
            }
        }

        public void Reuse(int prefabID, Vector2Int position, bool flip, float rotateValue = 0f)
        {
            if (!pools.ContainsKey(prefabID)) return;

            var pool = pools[prefabID];
            if (pool.Objects.Count == 0) return;

            ObjectInstance instance = pool.Objects.Dequeue();
            instance.Reuse(position, rotateValue, flip);
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

        public ushort GetPoolSize(int prefabID) => pools.ContainsKey(prefabID) ? pools[prefabID].Size : (ushort)0;
        public void IncreasePoolSize(int prefabID) { if (pools.ContainsKey(prefabID)) pools[prefabID].Size++; }

        private class ObjectInstance
        {
            private GameObject gameObject;
            private Transform transform;

            public ObjectInstance(GameObject obj, Transform parent)
            {
                gameObject = obj;
                transform = obj.transform;
                obj.transform.parent = parent;
                obj.SetActive(false);
            }

            public void Reuse(Vector2Int position, float rotateValue, bool flip)
            {
                transform.position = (Vector2)position;
                transform.rotation = Quaternion.Euler(0, flip ? 180 : 0, rotateValue);
                gameObject.SetActive(true);
            }

            public void Deactivate()
            {
                gameObject.SetActive(false);
            }
        }
    }
}