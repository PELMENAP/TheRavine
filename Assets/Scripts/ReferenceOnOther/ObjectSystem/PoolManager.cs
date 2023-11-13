using System.Collections.Generic;
using UnityEngine;

public class PoolManager : IPoolManager<GameObject>
{
    Dictionary<int, Queue<ObjectInstance>> poolDictionary = new Dictionary<int, Queue<ObjectInstance>>();
    Dictionary<int, Transform> poolObjectDictionary = new Dictionary<int, Transform>();

    public PoolManager()
    {

    }
    public void CreatePool(GameObject prefab, int poolSize = 1)
    {
        int poolKey = prefab.gameObject.GetInstanceID();

        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary.Add(poolKey, new Queue<ObjectInstance>());
            GameObject poolHolder = new GameObject(prefab.gameObject.name + " pool");
            poolHolder.isStatic = true;
            poolHolder.transform.parent = ObjectSystem.inst.transform;
            poolObjectDictionary.Add(poolKey, poolHolder.transform);
        }
        for (int i = 0; i < poolSize; i++)
        {
            ObjectInstance newObject = new ObjectInstance(ObjectSystem.inst.InstantiatePool(prefab.gameObject));
            poolDictionary[poolKey].Enqueue(newObject);
            newObject.SetParent(poolObjectDictionary[poolKey]);
            newObject.prefabID = poolKey;
        }
    }

    Queue<ObjectInstance> poDick;
    ObjectInstance objectToReuse;
    public void Reuse(int prefabID, Vector2 position)
    {
        poDick = poolDictionary[prefabID];
        objectToReuse = poDick.Dequeue();
        objectToReuse.Reuse(position);
        poDick.Enqueue(objectToReuse);
    }
}