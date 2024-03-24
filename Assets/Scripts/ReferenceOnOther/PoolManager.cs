using System.Collections.Generic;
using UnityEngine;
using TheRavine.Extentions;

namespace TheRavine.ObjectControl
{
    public class PoolManager : IPoolManager<GameObject>
    {
        private Transform parent;
        public PoolManager(Transform _parent)
        {
            parent = _parent;
        }
        Dictionary<int, LinkedList<ObjectInstance>> poolDictionary = new Dictionary<int, LinkedList<ObjectInstance>>();
        Dictionary<int, Pair<Transform, ushort>> poolObjectDictionary = new Dictionary<int, Pair<Transform, ushort>>();
        public void CreatePool(int poolKey, GameObject prefab, CreateInstance createInstance, ushort poolSize = 1)
        {
            if (!poolDictionary.ContainsKey(poolKey))
            {
                poolDictionary.Add(poolKey, new LinkedList<ObjectInstance>());
                GameObject poolHolder = new GameObject(prefab.name + " pool");
                poolHolder.isStatic = true;
                poolHolder.transform.parent = parent;
                poolObjectDictionary.Add(poolKey, new Pair<Transform, ushort>(poolHolder.transform, poolSize));
            }
            for (ushort i = 0; i < poolSize; i++)
            {
                ObjectInstance newObject = new ObjectInstance(createInstance?.Invoke(new Vector2(0, 0), prefab));
                poolDictionary[poolKey].AddFirst(newObject);
                newObject.SetParent(poolObjectDictionary[poolKey].First);
            }
        }
        public void Reuse(int prefabID, Vector2 position, bool flip, float rotateValue = 0f)
        {
            LinkedList<ObjectInstance> poDick = poolDictionary[prefabID];
            ObjectInstance objectToReuse = poDick.First.Value;
            poDick.RemoveFirst();
            objectToReuse.Reuse(position, rotateValue);
            if (flip)
                objectToReuse.Rotate(new Vector3(0, 180, 0));
            poDick.AddLast(objectToReuse);
        }
        public void Deactivate(int prefabID)
        {
            LinkedList<ObjectInstance> poDick = poolDictionary[prefabID];
            ObjectInstance objectToReuse = poDick.First.Value;
            poDick.RemoveFirst();
            objectToReuse.ActiveSelf(false);
            poDick.AddLast(objectToReuse);
        }
        public ushort GetPoolSize(int prefabID) => poolObjectDictionary[prefabID].Second;
        public void IncreasePoolSize(int prefabID) => poolObjectDictionary[prefabID] = new Pair<Transform, ushort>(poolObjectDictionary[prefabID].First, (ushort)(poolObjectDictionary[prefabID].Second + 1));
        public class ObjectInstance
        {
            private GameObject gameObject;
            private Transform transform;
            public ObjectInstance(GameObject objectInstance)
            {
                gameObject = objectInstance;
                transform = gameObject.transform;
                gameObject.isStatic = true;
                ActiveSelf(false);
            }
            public void Reuse(Vector2 position, float rotateValue = 0f)
            {
                if (rotateValue != 0f)
                {
                    // transform.RotateAround(Vector3.zero, Vector3.forward, rotateValue);
                    transform.Rotate(0, 0, rotateValue, Space.Self);
                }
                else
                {
                    transform.position = position;
                }
                ActiveSelf(true);
            }
            public void Rotate(Vector3 rotate)
            {
                transform.rotation = Quaternion.Euler(rotate);
            }
            public void SetParent(Transform _parent)
            {
                transform.parent = _parent;
            }
            public void ActiveSelf(bool active)
            {
                gameObject.SetActive(active);
            }
        }
    }
}