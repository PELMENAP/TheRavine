using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TheRavine.ObjectControl
{
    public class ObjectSystem : MonoBehaviour, ISetAble
    {
        private const int InitialInfoCapacity = 16;
        private const int InitialGlobalCapacity = 512;
        public GameObject InstantiatePoolObject(Vector3 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        public ObjectInfo[] _info;
        private Dictionary<int, ObjectInfo> info;
        public ObjectInfo GetPrefabInfo(int id)
        {
            if(!info.ContainsKey(id)) return info[0];
            return info[id];
        }
        private Dictionary<Vector2Int, ObjectInstInfo> global;
        public ObjectInstInfo GetGlobalObjectInstInfo(Vector2Int position)
        {
            if (!global.ContainsKey(position))
                return new ObjectInstInfo();
            return global[position];
        }

        public ObjectInfo GetGlobalObjectInfo(Vector2Int position)
        {
            ObjectInstInfo instInfo = GetGlobalObjectInstInfo(position);
            if (instInfo.PrefabID > 0)
                return GetPrefabInfo(instInfo.PrefabID);
            else
                return null;
        }
        public bool TryAddToGlobal(Vector2Int position, int _PrefabID, int _amount, InstanceType _objectType, bool _flip = false)
        {
            if(info == null || info.Count == 0) return false;
            if (global.ContainsKey(position))
                if (global[position].PrefabID == _PrefabID && global[position].objectType == InstanceType.Interactable)
                {
                    global[position] = new ObjectInstInfo(_PrefabID, global[position].amount + _amount, InstanceType.Interactable, global[position].flip); ;
                    return true;
                }
            ObjectInfo curdata = GetPrefabInfo(_PrefabID);
            ObjectInstInfo objectInfo = new(_PrefabID, _amount, _objectType, _flip);
            if (curdata.AdditionalOccupiedCells.Length == 0)
                return global.TryAdd(position, objectInfo);
            global[position] = objectInfo;
            for (byte i = 0; i < curdata.AdditionalOccupiedCells.Length; i++)
            {
                Vector2Int newPosition = position + curdata.AdditionalOccupiedCells[i];
                if(!global.ContainsKey(newPosition))
                    global[newPosition] = new ObjectInstInfo(-1, 0, InstanceType.Static, false, false);
            }
            return true;
        }
        private void AddToGlobal(Vector2Int position, int _PrefabID, ushort _amount, InstanceType _objectType, bool flip = false)
        {
            global[position] = new ObjectInstInfo(_PrefabID, _amount, _objectType, flip);
        }
        public bool RemoveFromGlobal(Vector2Int position)
        {
            ObjectInfo curdata = GetGlobalObjectInfo(position);
            if(curdata == null) return true;
            if (curdata.AdditionalOccupiedCells.Length == 0)
                return global.Remove(position);
            global.Remove(position);
            for (byte i = 0; i < curdata.AdditionalOccupiedCells.Length; i++)
                global.Remove(position + curdata.AdditionalOccupiedCells[i]);
            return true;
        }
        public bool ContainsGlobal(Vector2Int position) => global.ContainsKey(position);
        private PoolManager PoolManagerBase;
        public void CreatePool(int PrefabID, GameObject prefab, int poolSize = 1) => PoolManagerBase.CreatePool(PrefabID, prefab, InstantiatePoolObject, (ushort)poolSize);
        public void Reuse(int PrefabID, Vector2Int position, bool flip) => PoolManagerBase.Reuse(PrefabID, position, flip);
        public void Deactivate(int PrefabID) => PoolManagerBase.Deactivate(PrefabID);
        public ushort GetPoolSize(int PrefabID) => PoolManagerBase.GetPoolSize(PrefabID);
        public void IncreasePoolSize(int PrefabID) => PoolManagerBase.IncreasePoolSize(PrefabID);
        public void SetUp(ISetAble.Callback callback)
        {
            info = new Dictionary<int, ObjectInfo>(InitialInfoCapacity);
            global = new Dictionary<Vector2Int, ObjectInstInfo>(InitialGlobalCapacity);
            PoolManagerBase = new PoolManager(this.transform);

            for (byte i = 0; i < _info.Length; i++)
            {
                info[_info[i].PrefabID] = _info[i];
            }

            FirstInstance().Forget();
            callback?.Invoke();
        }
        private async UniTaskVoid FirstInstance()
        {
            for (byte i = 0; i < _info.Length; i++)
            {
                CreatePool(_info[i].PrefabID, _info[i].ObjectPrefab, _info[i].InitialPoolSize);
                await UniTask.Delay(10);
            }
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            info.Clear();
            global.Clear();
            callback?.Invoke();
        }
    }

    public struct ObjectInstInfo
    {
        public int amount;
        public readonly int PrefabID;
        public readonly InstanceType objectType;
        public bool flip, isExist;
        public ObjectInstInfo(int _PrefabID = -1, int _amount = 1, InstanceType _objectType = InstanceType.Static, bool _flip = false, bool _isExist = true)
        {
            amount = _amount;
            PrefabID = _PrefabID;
            objectType = _objectType;
            flip = _flip;
            isExist = _isExist;
        }
    }
}