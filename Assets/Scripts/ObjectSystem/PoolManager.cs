using System.Collections.Generic;
using UnityEngine;

public class PoolManager : IPoolManager<GameObject>
{
    Dictionary<int, Queue<ObjectInstance>> poolDictionary = new Dictionary<int, Queue<ObjectInstance>>();
    Dictionary<int, Transform> poolObjectDictionary = new Dictionary<int, Transform>();
    public void CreatePool(GameObject prefab, int poolSize = 1)
    {
        int poolKey = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary.Add(poolKey, new Queue<ObjectInstance>());
            GameObject poolHolder = new GameObject(prefab.name + " pool");
            poolHolder.isStatic = true;
            poolHolder.transform.parent = ObjectSystem.inst.transform;
            poolObjectDictionary.Add(poolKey, poolHolder.transform);
        }
        for (int i = 0; i < poolSize; i++)
        {
            ObjectInstance newObject = new ObjectInstance(ObjectSystem.inst.InstantiatePoolObject(new Vector2(0, 0), prefab));
            poolDictionary[poolKey].Enqueue(newObject);
            newObject.SetParent(poolObjectDictionary[poolKey]);
        }
    }
    public void Reuse(int prefabID, Vector2 position)
    {
        Queue<ObjectInstance> poDick = poolDictionary[prefabID];
        ObjectInstance objectToReuse = poDick.Dequeue();
        objectToReuse.Reuse(position);
        poDick.Enqueue(objectToReuse);
    }

    public class ObjectInstance
    {
        public GameObject gameObject;
        private Transform transform;
        public ObjectInstance(GameObject objectInstance)
        {
            gameObject = objectInstance;
            transform = gameObject.transform;
            gameObject.isStatic = true;
            ActiveSelf(false);
        }
        public void Reuse(Vector2 position)
        {
            transform.position = position;
            ActiveSelf(true);
        }
        public void SetParent(Transform parent)
        {
            transform.parent = parent;
        }
        public void ActiveSelf(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}