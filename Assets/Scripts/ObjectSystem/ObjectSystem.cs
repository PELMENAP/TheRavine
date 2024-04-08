using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

using TheRavine.Services;

namespace TheRavine.ObjectControl
{
    public class ObjectSystem : MonoBehaviour, ISetAble
    {
        public GameObject InstantiatePoolObject(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        public ObjectInfo[] _info;
        private Dictionary<int, ObjectInfo> info;
        public ObjectInfo GetPrefabInfo(int id) => info[id];
        //
        private Dictionary<Vector2, ObjectInstInfo> global;
        public ObjectInstInfo GetGlobalObjectInstInfo(Vector2 position)
        {
            if (!global.ContainsKey(position))
                return new ObjectInstInfo();
            return global[position];
        }

        public ObjectInfo GetGlobalObjectInfo(Vector2 position)
        {
            ObjectInstInfo instInfo = GetGlobalObjectInstInfo(position);
            if (instInfo.isExist)
                return GetPrefabInfo(instInfo.prefabID);
            else
                return null;
        }
        public bool TryAddToGlobal(Vector2 position, int _prefabID, ushort _amount, InstanceType _objectType, bool _flip = false)
        {
            if(info.Count == 0)
                return false;
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
            {
                Vector2 newPosition = position + curdata.addspace[i];
                if(!global.ContainsKey(newPosition))
                    global[newPosition] = new ObjectInstInfo(-1, 0, InstanceType.Static, false, false);
            }
            return true;
        }
        private void AddToGlobal(Vector2 position, int _prefabID, ushort _amount, InstanceType _objectType, bool flip = false)
        {
            global[position] = new ObjectInstInfo(_prefabID, _amount, _objectType, flip);
        }
        public bool RemoveFromGlobal(Vector2 position)
        {
            ObjectInstInfo objectInfo = GetGlobalObjectInstInfo(position);
            if (objectInfo.prefabID == -1 && !objectInfo.isExist)
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
        public void CreatePool(int prefabID, GameObject prefab, ushort poolSize = 1) => PoolManagerBase.CreatePool(prefabID, prefab, InstantiatePoolObject, poolSize);
        public void Reuse(int prefabID, Vector2 position, bool flip, float rotateValue) => PoolManagerBase.Reuse(prefabID, position, flip, rotateValue);
        public void Deactivate(int prefabID) => PoolManagerBase.Deactivate(prefabID);
        public ushort GetPoolSize(int prefabID) => PoolManagerBase.GetPoolSize(prefabID);
        public void IncreasePoolSize(int prefabID) => PoolManagerBase.IncreasePoolSize(prefabID);
        //
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            info = new Dictionary<int, ObjectInfo>(16);
            global = new Dictionary<Vector2, ObjectInstInfo>(512);
            PoolManagerBase = new PoolManager(this.transform);
            for (byte i = 0; i < _info.Length; i++)
            {
                info[_info[i].prefab.GetInstanceID()] = _info[i];
                // print(_info[i].prefab.GetInstanceID());
            }
            FirstInstance().Forget();
            callback?.Invoke();
        }
        private async UniTaskVoid FirstInstance()
        {
            for (byte i = 0; i < _info.Length; i++)
            {
                CreatePool(_info[i].prefab.GetInstanceID(), _info[i].prefab, _info[i].poolSize);
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
        public readonly int prefabID;
        public readonly InstanceType objectType;
        public bool flip, isExist;
        public ObjectInstInfo(int _prefabID = -1, ushort _amount = 1, InstanceType _objectType = InstanceType.Static, bool _flip = false, bool _isExist = true)
        {
            amount = _amount;
            prefabID = _prefabID;
            objectType = _objectType;
            flip = _flip;
            isExist = _isExist;
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