using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    private InterObjectManager objectManager;
    public bool SetObjectByPosition(Vector2 position, string id, int amount, ObjectInstance prefab) => objectManager.SetObjectByPosition(position, id, amount, prefab);
    public Triple<string, int, ObjectInstance> GetObjectByPosition(Vector2 position) => objectManager.GetObjectByPosition(position);
    Dictionary<int, PoolInfo> poolDictionary = new Dictionary<int, PoolInfo>();
    Dictionary<int, Transform> poolObjectDictionary = new Dictionary<int, Transform>();

    public static PoolManager inst;

    private void Awake()
    {
        inst = this;
        objectManager = new InterObjectManager(this);
    }

    public void CreatePool(GameObject prefab, int poolSize = 1)
    {
        int poolKey = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary.Add(poolKey, new PoolInfo(new Dictionary<Vector2, ObjectInstance>(), new Queue<ObjectInstance>()));
            GameObject poolHolder = new GameObject(prefab.name + " pool");
            poolHolder.isStatic = true;
            poolHolder.transform.parent = this.transform;
            poolObjectDictionary.Add(poolKey, poolHolder.transform);
        }
        for (int i = 0; i < poolSize; i++)
        {
            ObjectInstance newObject = new ObjectInstance(Instantiate(prefab) as GameObject);
            poolDictionary[poolKey].useable.Enqueue(newObject);
            newObject.SetParent(poolObjectDictionary[poolKey]);
            newObject.prefabID = poolKey;
        }
    }

    public void DeleteObjectFromPosition(int prefabID, Vector2 position)
    {

    }

    public void ReuseObjectToPosition(int prefabID, Vector2 position)
    {
        PoolInfo poDick = poolDictionary[prefabID];
        ObjectInstance objectToReuse = poDick.useable.Dequeue();
        poDick.used.Add(position, objectToReuse);
        objectToReuse.Reuse(position);
    }

    public void MakeObjectInvisible(Vector2 position)
    {
        Triple<string, int, ObjectInstance> triple = GetObjectByPosition(position);
        triple.Third.gameObject.SetActive(false);
    }

    public ObjectInstance GetObjectByID(int prefabID) => poolDictionary[prefabID].useable.Peek();
}

public struct PoolInfo
{
    public Dictionary<Vector2, ObjectInstance> used;
    public Queue<ObjectInstance> useable;

    public PoolInfo(Dictionary<Vector2, ObjectInstance> _used, Queue<ObjectInstance> _useable)
    {
        used = _used;
        useable = _useable;
    }
}
public class ObjectInstance
{
    public GameObject gameObject;
    public bool reusable;
    public int prefabID;
    private Transform transform;
    public ObjectInstance(GameObject objectInstance)
    {
        gameObject = objectInstance;
        transform = gameObject.transform;
        gameObject.isStatic = true;
        gameObject.SetActive(false);
    }

    public void Reuse(Vector2 position)
    {
        gameObject.SetActive(true);
        transform.position = position;
    }

    public void Delete()
    {
        gameObject.SetActive(false);
    }

    public void SetParent(Transform parent)
    {
        transform.parent = parent;
    }
}