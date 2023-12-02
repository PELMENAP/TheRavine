using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;

public class ObjectSystem : MonoBehaviour, ISetAble
{
    public static ObjectSystem inst;
    public PrefabInfo[] _info;
    private Dictionary<int, PrefabData> info = new Dictionary<int, PrefabData>(32);
    public PrefabData GetPrefabInfo(int id) => info[id];
    //
    private Dictionary<Vector2, ObjectInstInfo> global = new Dictionary<Vector2, ObjectInstInfo>(512);
    public ObjectInstInfo GetGlobalObjectInfo(Vector2 position)
    {
        if (!global.ContainsKey(position))
            return new ObjectInstInfo();
        return global[position];
    }
    public bool AddToGlobal(Vector2 position, ushort _prefabID, string _name, ushort _amount, InstanceType _objectType) => global.TryAdd(position, new ObjectInstInfo(_prefabID, _name, _amount, _objectType));
    public bool RemoveFromGlobal(Vector2 position) => global.Remove(position);
    public bool ContainsGlobal(Vector2 position) => global.ContainsKey(position);
    //
    private Dictionary<Vector2, ObjectInstInfo> changes = new Dictionary<Vector2, ObjectInstInfo>();
    public bool Changed(Vector2 position) => changes.ContainsKey(position);
    // private List<Transform> trans = new List<Transform>();
    //
    private IPoolManager<GameObject> PoolManagerBase = new PoolManager();
    public void CreatePool(GameObject prefab, int poolSize) => PoolManagerBase.CreatePool(prefab, poolSize);
    public void Reuse(int prefabID, Vector2 position) => PoolManagerBase.Reuse(prefabID, position);
    public void Deactivate(int prefabID) => PoolManagerBase.Deactivate(prefabID);
    public void SetUp(ref bool result)
    {
        inst = this;
        for (int i = 0; i < _info.Length; i++)
            info[_info[i].prefab.GetInstanceID()] = new PrefabData(_info[i].name, _info[i].amount, _info[i].poolSize, _info[i].type, _info[i].prefab);
        // NAL().Forget();
        FirstInstance().Forget();
    }

    private async UniTaskVoid FirstInstance()
    {
        for (int i = 0; i < _info.Length; i++)
        {
            CreatePool(_info[i].prefab, _info[i].poolSize);
            await UniTask.Delay(500);
            FaderOnTransit.instance.SetLogs("Созданы: " + _info[i].prefab.name);
        }
    }

    // public GameObject InstantiatePool(GameObject prefab) => Instantiate(prefab);
    // public Transform InstantiatePoolByPosition(Vector2 position, GameObject prefab)
    // {
    //     Transform gobject = Instantiate(prefab, position, Quaternion.identity, this.transform).transform;
    //     return gobject;
    // }

    public GameObject InstantiatePoolObject(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);

    public void RotateBasis()
    {
        // StartCoroutine(Rotation());
    }

    public Transform Player;
    // private IEnumerator Rotation()
    // {
    // float ratateValue = 1f;
    // while (this.transform.rotation.z > -0.7f)
    // {
    //     foreach (Transform item in trans)
    //         item.Rotate(0, 0, ratateValue, Space.Self);
    //     this.transform.Rotate(0, 0, -ratateValue, Space.Self);
    //     Player.RotateAround(Vector3.zero, Vector3.forward, -ratateValue);
    //     Player.Rotate(0, 0, ratateValue, Space.Self);
    //     yield return new WaitForFixedUpdate();
    // }
    // yield return new WaitForFixedUpdate();
    // }

    public Dictionary<Vector2Int, Transform> nettleEntity = new Dictionary<Vector2Int, Transform>();
    public List<Transform> nettle = new List<Transform>();
    public List<Transform> potentialNettle = new List<Transform>();
    public List<Vector2Int> potentialNettlePosition = new List<Vector2Int>();
    public List<Transform> deadNettle = new List<Transform>();
    public List<Vector2Int> deadNettlePosition = new List<Vector2Int>();
    public int maxcount, around, distance;
    [SerializeField, Range(0, 1)] private float chance;
    private async UniTaskVoid NAL()
    {
        int countOfCycle = 0;
        foreach (Transform item in nettle)
        {
            nettleEntity[new Vector2Int((int)item.position.x, (int)item.position.y)] = item;
        }
        while (true)
        {
            foreach (Transform item in nettle)
            {
                countOfCycle++;
                // if (countOfCycle % 3 == 0)
                //     continue;
                int xpos = (int)item.position.x, ypos = (int)item.position.y;
                int count = 0;
                for (int x = xpos - around; x <= xpos + around; x++)
                    for (int y = ypos - around; y <= ypos + around; y++)
                        if (nettleEntity.ContainsKey(new Vector2Int(x, y)))
                            count++;
                if (count > maxcount)
                {
                    deadNettle.Add(item);
                    deadNettlePosition.Add(new Vector2Int(xpos, ypos));
                }
                else
                {
                    int newx, newy;
                    newx = Random.Range(xpos - distance, xpos + distance);
                    newy = Random.Range(ypos - distance, ypos + distance);
                    if (!nettleEntity.ContainsKey(new Vector2Int(newx, newy)) && Random.value < chance)
                    {
                        potentialNettle.Add(item);
                        potentialNettlePosition.Add(new Vector2Int(newx, newy));
                    }
                }
            }
            for (int i = 0; i < potentialNettle.Count; i++)
            {
                Transform gobject = InstantiatePoolObject(new Vector2(potentialNettlePosition[i].x, potentialNettlePosition[i].y), potentialNettle[i].gameObject).transform;
                nettleEntity[potentialNettlePosition[i]] = gobject;
                nettle.Add(gobject);
                await UniTask.Delay(100);
            }
            for (int i = 0; i < deadNettle.Count; i++)
            {
                nettleEntity.Remove(deadNettlePosition[i]);
                nettle.Remove(deadNettle[i]);
                Destroy(deadNettle[i].gameObject);
                await UniTask.Delay(100);
            }
            potentialNettlePosition.Clear();
            potentialNettle.Clear();
            deadNettlePosition.Clear();
            deadNettle.Clear();
            await UniTask.Delay(100);
        }
    }
}

public struct ObjectInstInfo
{
    public string name { get; private set; }
    public ushort amount;
    public ushort prefabID { get; private set; }
    public InstanceType objectType { get; private set; }
    public ObjectInstInfo(ushort _prefabID = 0, string _name = "therivinetop", ushort _amount = 1, InstanceType _objectType = InstanceType.Static)
    {
        name = _name;
        amount = _amount;
        prefabID = _prefabID;
        objectType = _objectType;
    }
}

public class PrefabData
{
    public string name;
    public ushort amount, poolSize;
    public InstanceType type;
    public GameObject prefab;

    public PrefabData(string _name, ushort _amount, ushort _poolSize, InstanceType _type, GameObject _prefab)
    {
        name = _name;
        amount = _amount;
        poolSize = _poolSize;
        type = _type;
        prefab = _prefab;
    }
}

[System.Serializable]
public struct PrefabInfo
{
    public string name;
    public ushort amount, poolSize;
    public InstanceType type;
    public GameObject prefab;
}