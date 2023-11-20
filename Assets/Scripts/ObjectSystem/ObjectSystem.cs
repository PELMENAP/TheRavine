using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;

public class ObjectSystem : MonoBehaviour
{
    public static ObjectSystem inst;
    [SerializeField] private GameObject[] _prefab;
    [SerializeField] private PrefabInfo[] _info;
    private Dictionary<int, PrefabInfo> info = new Dictionary<int, PrefabInfo>();
    public PrefabInfo GetPrefabInfo(int id) => info[id];
    private Dictionary<Vector2, ObjectInstInfo> global = new Dictionary<Vector2, ObjectInstInfo>();
    private Dictionary<Vector2, ObjectInstInfo> changes = new Dictionary<Vector2, ObjectInstInfo>();
    public bool Changed(Vector2 position) => changes.ContainsKey(position);
    // private List<Transform> trans = new List<Transform>();
    private IPoolManager<GameObject> PoolManagerBase = new PoolManager();
    public void AddToGlobal(Vector2 position, string _name, int _amount, InstanceType _objectType) => global[position] = new ObjectInstInfo(_name, _amount, _objectType);

    public void CreatePool(GameObject prefab, int poolSize) => PoolManagerBase.CreatePool(prefab, poolSize);
    public void Reuse(int prefabID, Vector2 position) => PoolManagerBase.Reuse(prefabID, position);
    private void Awake()
    {
        inst = this;
        for (int i = 0; i < _info.Length; i++)
            info[_prefab[i].GetInstanceID()] = _info[i];
        // NAL().Forget();
        FirstInstance().Forget();
    }

    private async UniTaskVoid FirstInstance()
    {
        for (int i = 0; i < _info.Length; i++)
        {
            CreatePool(_prefab[i], 50);
            await UniTask.Delay(100);
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

public class ObjectInstInfo
{
    private string name;
    private int amount;
    private InstanceType objectType;
    public ObjectInstInfo(string _name, int _amount, InstanceType _objectType)
    {
        name = _name;
        amount = _amount;
        objectType = _objectType;
    }
    public string GetName() => name;
    public int GetAmount() => amount;
    public InstanceType GetObjectType() => objectType;
    public bool SetAmount(int _amount)
    {
        amount += _amount;
        if (amount < 0)
            return false;
        return true;
    }
}

[System.Serializable]
public struct PrefabInfo
{
    public string name;
    public int amount;
    public InstanceType type;
}