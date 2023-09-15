using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    Dictionary<int, LinkedList<ObjectInstance>> poolDictionary = new Dictionary<int, LinkedList<ObjectInstance>>();
    Dictionary<int, Transform> poolObjectDictionary = new Dictionary<int, Transform>();

    public static PoolManager instance;

    private void Awake()
    {
        instance = this;
    }

    public void CreatePool(GameObject prefab, int poolSize, bool newObs = false)
    {
        int poolKey = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(poolKey) || newObs)
        {
            if (!newObs)
            {
                poolDictionary.Add(poolKey, new LinkedList<ObjectInstance>());
                GameObject poolHolder = new GameObject(prefab.name + " pool");
                poolHolder.isStatic = true;
                poolHolder.transform.parent = this.transform;
                poolObjectDictionary.Add(poolKey, poolHolder.transform);
            }
            for (int i = 0; i < poolSize; i++)
            {
                ObjectInstance newObject = new ObjectInstance(Instantiate(prefab) as GameObject);
                poolDictionary[poolKey].AddFirst(newObject);
                newObject.SetParent(poolObjectDictionary[poolKey]);
            }
        }
    }

    public void ReuseObject(GameObject prefab, Vector2 position, bool visible)
    {
        LinkedList<ObjectInstance> poDick = poolDictionary[prefab.GetInstanceID()];
        ObjectInstance objectToReuse = poDick.First?.Value;
        poDick.RemoveFirst();
        poDick.AddLast(objectToReuse);
        objectToReuse.Reuse(position, visible);
    }

    public class ObjectInstance
    {
        public GameObject gameObject;
        Transform transform;
        bool hasPoolObjectComponent;
        // PoolObject poolObjectScript;
        public ObjectInstance(GameObject objectInstance)
        {
            gameObject = objectInstance;
            transform = gameObject.transform;
            gameObject.isStatic = true;
            if (gameObject.tag == "Enviroment")
            {
            }
            // if (gameObject.GetComponent<PoolObject>())
            // {
            //     hasPoolObjectComponent = true;
            //     poolObjectScript = gameObject.GetComponent<PoolObject>();
            // }
        }

        public void Reuse(Vector2 position, bool visible)
        {
            gameObject.SetActive(visible);
            transform.position = position;
            // if (hasPoolObjectComponent)
            // {
            //     poolObjectScript.OnObjectReuse();
            // }
        }

        public void SetParent(Transform parent)
        {
            transform.parent = parent;
            gameObject.SetActive(false);
        }
    }
}
