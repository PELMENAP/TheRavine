using System.Collections.Generic;
using UnityEngine;

public class PoolManager : IPoolManager<GameObject>
{
    private Transform parent;
    public PoolManager(Transform _parent)
    {
        parent = _parent;
    }
    Dictionary<int, LinkedList<ObjectInstance>> poolDictionary = new Dictionary<int, LinkedList<ObjectInstance>>();
    Dictionary<int, Transform> poolObjectDictionary = new Dictionary<int, Transform>();
    public void CreatePool(GameObject prefab, CreateInstance createInstance, int poolSize = 1)
    {
        int poolKey = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary.Add(poolKey, new LinkedList<ObjectInstance>());
            GameObject poolHolder = new GameObject(prefab.name + " pool");
            poolHolder.isStatic = true;
            poolHolder.transform.parent = parent;
            poolObjectDictionary.Add(poolKey, poolHolder.transform);
        }
        for (ushort i = 0; i < poolSize; i++)
        {
            ObjectInstance newObject = new ObjectInstance(createInstance?.Invoke(new Vector2(0, 0), prefab));
            poolDictionary[poolKey].AddFirst(newObject);
            newObject.SetParent(poolObjectDictionary[poolKey]);
        }
    }
    public void Reuse(int prefabID, Vector2 position, float rotateValue = 0f)
    {
        LinkedList<ObjectInstance> poDick = poolDictionary[prefabID];
        ObjectInstance objectToReuse = poDick.First.Value;
        poDick.RemoveFirst();
        objectToReuse.Reuse(position, rotateValue);
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