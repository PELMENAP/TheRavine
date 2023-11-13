using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ObjectSystem : MonoBehaviour
{
    public Dictionary<Vector2, IObject> changes = new Dictionary<Vector2, IObject>();
    private List<Transform> trans = new List<Transform>();
    public Transform Player;
    public static ObjectSystem inst;
    public IPoolManager<GameObject> PoolManagerBase = new PoolManager();
    private void Awake()
    {
        inst = this;
        NAL().Forget();
    }

    public GameObject InstantiatePool(GameObject prefab) => Instantiate(prefab);
    public Transform InstantiatePoolByPosition(Vector2 position, GameObject prefab)
    {
        Transform gobject = Instantiate(prefab, position, Quaternion.identity, this.transform).transform;
        trans.Add(gobject);
        return gobject;
    }

    public void RotateBasis()
    {
        StartCoroutine(Rotation());
    }

    private IEnumerator Rotation()
    {
        float ratateValue = 1f;
        while (this.transform.rotation.z > -0.7f)
        {
            foreach (Transform item in trans)
                item.Rotate(0, 0, ratateValue, Space.Self);
            this.transform.Rotate(0, 0, -ratateValue, Space.Self);
            Player.RotateAround(Vector3.zero, Vector3.forward, -ratateValue);
            Player.Rotate(0, 0, ratateValue, Space.Self);
            yield return new WaitForFixedUpdate();
        }
        yield return new WaitForFixedUpdate();
    }

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
                Transform gobject = InstantiatePoolByPosition(new Vector2(potentialNettlePosition[i].x, potentialNettlePosition[i].y), potentialNettle[i].gameObject);
                nettleEntity[potentialNettlePosition[i]] = gobject;
                nettle.Add(gobject);
                yield return new WaitForSeconds(0.1f);
            }
            for (int i = 0; i < deadNettle.Count; i++)
            {
                nettleEntity.Remove(deadNettlePosition[i]);
                nettle.Remove(deadNettle[i]);
                Destroy(deadNettle[i].gameObject);
                yield return new WaitForSeconds(0.1f);
            }
            potentialNettlePosition.Clear();
            potentialNettle.Clear();
            deadNettlePosition.Clear();
            deadNettle.Clear();
            yield return new WaitForSeconds(3f);
        }
    }
}
public class InterObject
{
    public string id;
    public int amount, prefabID;
    public GameObject gameObject;
    public InterObject()
    {
    }
}

public class StaticObject
{
    public int amount, prefabID;
    public GameObject gameObject;
    private Transform transform;
    public StaticObject(GameObject objectInstance)
    {
        gameObject = objectInstance;
        transform = gameObject.transform;
    }

    public void Reuse(Vector2 position)
    {
        gameObject.SetActive(true);
        transform.position = position;
    }

    public void SetParent(Transform parent)
    {
        transform.parent = parent;
        gameObject.isStatic = true;
        gameObject.SetActive(false);

    }
}

public class ObjectInstance
{
    public GameObject gameObject;
    public int prefabID;
    private Transform transform;
    public ObjectInstance(GameObject objectInstance)
    {
        gameObject = objectInstance;
        transform = gameObject.transform;
    }

    public void Reuse(Vector2 position)
    {
        gameObject.SetActive(true);
        transform.position = position;
    }

    public void SetParent(Transform parent)
    {
        transform.parent = parent;
        gameObject.isStatic = true;
        gameObject.SetActive(false);
    }
}