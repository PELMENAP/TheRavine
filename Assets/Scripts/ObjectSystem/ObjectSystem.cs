using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;

using TheRavine.Services;

namespace TheRavine.ObjectControl
{
    public class ObjectSystem : MonoBehaviour, ISetAble
    {
        public GameObject InstantiatePoolObject(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        public ObjectInfo[] _info;
        private Dictionary<string, ObjectInfo> info;
        public ObjectInfo GetPrefabInfo(string id) => info[id];
        //
        private Dictionary<Vector2, ObjectInstInfo> global;
        public ObjectInstInfo GetGlobalObjectInfo(Vector2 position)
        {
            if (!global.ContainsKey(position))
                return new ObjectInstInfo();
            return global[position];
        }
        public bool TryAddToGlobal(Vector2 position, string _prefabID, ushort _amount, InstanceType _objectType, bool _flip = false)
        {
            if (global.ContainsKey(position))
                if (global[position].prefabID == _prefabID && global[position].objectType == InstanceType.Inter)
                {
                    global[position] = new ObjectInstInfo(global[position].prefabID, (ushort)(global[position].amount + _amount), global[position].objectType, global[position].flip); ;
                    return true;
                }
            ObjectInfo curdata = GetPrefabInfo(_prefabID);
            ObjectInstInfo objectInfo = new ObjectInstInfo(_prefabID, _amount, _objectType, _flip);
            if (curdata.addspace.Length == 0)
                return global.TryAdd(position, objectInfo);
            global[position] = objectInfo;
            for (byte i = 0; i < curdata.addspace.Length; i++)
                global[position + curdata.addspace[i]] = new ObjectInstInfo();
            return true;
        }
        private void AddToGlobal(Vector2 position, string _prefabID, ushort _amount, InstanceType _objectType, bool flip = false)
        {
            global[position] = new ObjectInstInfo(_prefabID, _amount, _objectType, flip);
        }
        public bool RemoveFromGlobal(Vector2 position)
        {
            ObjectInstInfo objectInfo = GetGlobalObjectInfo(position);
            if (objectInfo.prefabID == "")
                return true;
            ObjectInfo curdata = GetPrefabInfo(objectInfo.prefabID);
            if (curdata.addspace.Length == 0)
                return global.Remove(position);
            global.Remove(position);
            for (byte i = 0; i < curdata.addspace.Length; i++)
                global.Remove(position + curdata.addspace[i]);
            return true;
        }
        public bool ContainsGlobal(Vector2 position) => global.ContainsKey(position);
        //
        // private Dictionary<Vector2, ObjectInstInfo> changes = new Dictionary<Vector2, ObjectInstInfo>();
        // public bool Changed(Vector2 position) => changes.ContainsKey(position);
        //
        private IPoolManager<GameObject> PoolManagerBase;
        public void CreatePool(string prefabID, GameObject prefab, ushort poolSize = 1) => PoolManagerBase.CreatePool(prefabID, prefab, InstantiatePoolObject, poolSize);
        public void Reuse(string prefabID, Vector2 position, bool flip, float rotateValue) => PoolManagerBase.Reuse(prefabID, position, flip, rotateValue);
        public void Deactivate(string prefabID) => PoolManagerBase.Deactivate(prefabID);
        public ushort GetPoolSize(string prefabID) => PoolManagerBase.GetPoolSize(prefabID);
        public void IncreasePoolSize(string prefabID) => PoolManagerBase.IncreasePoolSize(prefabID);
        //
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            info = new Dictionary<string, ObjectInfo>(16);
            global = new Dictionary<Vector2, ObjectInstInfo>(512);
            PoolManagerBase = new PoolManager(this.transform);
            for (byte i = 0; i < _info.Length; i++)
            {
                info[_info[i].id] = _info[i];
                // print(_info[i].prefab.GetInstanceID());
            }
            FirstInstance().Forget();
            callback?.Invoke();
        }
        private async UniTaskVoid FirstInstance()
        {
            for (byte i = 0; i < _info.Length; i++)
            {
                CreatePool(_info[i].id, _info[i].prefab, _info[i].poolSize);
                await UniTask.Delay(100);
                //FaderOnTransit.instance.SetLogs("Созданы: " + _info[i].prefab.id);
            }
        }

        public void BreakUp()
        {
            info.Clear();
            global.Clear();
            // changes.Clear();
        }
    }

    public struct ObjectInstInfo
    {
        public ushort amount;
        public readonly string prefabID;
        public readonly InstanceType objectType;
        public bool flip;
        public ObjectInstInfo(string _prefabID = "", ushort _amount = 1, InstanceType _objectType = InstanceType.Static, bool _flip = false)
        {
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