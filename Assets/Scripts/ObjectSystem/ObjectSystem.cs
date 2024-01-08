using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;
public delegate GameObject CreateInstance(Vector2 position, GameObject prefab);
namespace TheRavine.ObjectControl
{
    public class ObjectSystem : MonoBehaviour, ISetAble
    {
        public GameObject InstantiatePoolObject(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        public ObjectInfo[] _info;
        private Dictionary<int, ObjectInfo> info = new Dictionary<int, ObjectInfo>(16);
        public ObjectInfo GetPrefabInfo(int id) => info[id];
        //
        private Dictionary<Vector2, ObjectInstInfo> global = new Dictionary<Vector2, ObjectInstInfo>(128);
        public ObjectInstInfo GetGlobalObjectInfo(Vector2 position)
        {
            if (!global.ContainsKey(position))
                return new ObjectInstInfo();
            return global[position];
        }
        public bool TryAddToGlobal(Vector2 position, int _prefabID, string _name, ushort _amount, InstanceType _objectType, bool flip = false)
        {
            ObjectInfo curdata = GetPrefabInfo(_prefabID);
            ObjectInstInfo objectInfo = new ObjectInstInfo(_prefabID, _name, _amount, _objectType, flip);
            if (curdata.addspace.Length == 0)
                return global.TryAdd(position, objectInfo);
            global[position] = objectInfo;
            for (int i = 0; i < curdata.addspace.Length; i++)
                global[position + curdata.addspace[i]] = new ObjectInstInfo();
            return true;
        }
        public void AddToGlobal(Vector2 position, int _prefabID, string _name, ushort _amount, InstanceType _objectType, bool flip = false)
        {
            global[position] = new ObjectInstInfo(_prefabID, _name, _amount, _objectType, flip);
        }
        public bool RemoveFromGlobal(Vector2 position)
        {
            ObjectInstInfo objectInfo = GetGlobalObjectInfo(position);
            if (objectInfo.prefabID == 0)
                return true;
            ObjectInfo curdata = GetPrefabInfo(objectInfo.prefabID);
            if (curdata.addspace.Length == 0)
                return global.Remove(position);
            for (int i = 0; i < curdata.addspace.Length; i++)
                global.Remove(position + curdata.addspace[i]);
            return true;
        }
        public bool ContainsGlobal(Vector2 position) => global.ContainsKey(position);
        //
        private Dictionary<Vector2, ObjectInstInfo> changes = new Dictionary<Vector2, ObjectInstInfo>();
        public bool Changed(Vector2 position) => changes.ContainsKey(position);
        //
        private IPoolManager<GameObject> PoolManagerBase;
        public void CreatePool(GameObject prefab, ushort poolSize = 1)
        {
            PoolManagerBase.CreatePool(prefab, InstantiatePoolObject, poolSize);
        }
        public void Reuse(int prefabID, Vector2 position, bool flip, float rotateValue) => PoolManagerBase.Reuse(prefabID, position, flip, rotateValue);
        public void Deactivate(int prefabID) => PoolManagerBase.Deactivate(prefabID);
        public ushort GetPoolSize(int prefabID) => PoolManagerBase.GetPoolSize(prefabID);
        public void IncreasePoolSize(int prefabID) => PoolManagerBase.IncreasePoolSize(prefabID);
        //
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            PoolManagerBase = new PoolManager(this.transform);
            for (int i = 0; i < _info.Length; i++)
            {
                info[_info[i].prefab.GetInstanceID()] = _info[i];
                // print(_info[i].prefab.GetInstanceID());
            }
            FirstInstance().Forget();
            callback?.Invoke();
        }
        private async UniTaskVoid FirstInstance()
        {
            for (int i = 0; i < _info.Length; i++)
            {
                CreatePool(_info[i].prefab, _info[i].poolSize);
                await UniTask.Delay(100);
                //FaderOnTransit.instance.SetLogs("Созданы: " + _info[i].prefab.name);
            }
        }
    }

    public class ObjectInstInfo
    {
        public string name { get; private set; }
        public ushort amount;
        public int prefabID { get; private set; }
        public InstanceType objectType { get; private set; }
        public bool flip;
        public ObjectInstInfo(int _prefabID = -1, string _name = "therivinetop", ushort _amount = 1, InstanceType _objectType = InstanceType.Static, bool _flip = false)
        {
            name = _name;
            amount = _amount;
            prefabID = _prefabID;
            objectType = _objectType;
            flip = _flip;
        }
    }
}
public enum InstanceType
{
    Static,
    Inter,
    Struct
}

public enum BehaviourType
{
    None,
    NAL,
    GROW
}