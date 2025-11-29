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
        private readonly Dictionary<int, ObjectInfo> info = new(InitialInfoCapacity);
        private readonly Dictionary<Vector2Int, ObjectInstInfo> global = new (InitialGlobalCapacity);
        public ObjectInfo GetPrefabInfo(int id)
        {
            if(!info.ContainsKey(id)) return info[0];
            return info[id];
        }
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
        public bool TryAddToGlobal(Vector2Int position, Vector3 realPosition, int _PrefabID, int _amount, InstanceType _objectType)
        {
            if(info == null || info.Count == 0) return false;
            if (global.ContainsKey(position))
                if (global[position].PrefabID == _PrefabID && global[position].objectType == InstanceType.Interactable)
                {
                    global[position] = new ObjectInstInfo(realPosition, _PrefabID, global[position].amount + _amount, InstanceType.Interactable); ;
                    return true;
                }
            ObjectInfo currentData = GetPrefabInfo(_PrefabID);
            ObjectInstInfo objectInfo = new(realPosition, _PrefabID, _amount, _objectType);

            // if(currentData.AdditionalOccupiedCells == null)
            // {
            //     Debug.Log(currentData.ObjectName);
            //     return false;
            // }

            if (currentData.AdditionalOccupiedCells.Length == 0)
                return global.TryAdd(position, objectInfo);
            global[position] = objectInfo;
            for (byte i = 0; i < currentData.AdditionalOccupiedCells.Length; i++)
            {
                Vector2Int newPosition = position + currentData.AdditionalOccupiedCells[i];
                if(!global.ContainsKey(newPosition))
                    global[newPosition] = new ObjectInstInfo(Vector3.zero, -1, 0, InstanceType.Static, false);
            }
            return true;
        }
        public bool RemoveFromGlobal(Vector2Int position)
        {
            ObjectInfo currentData = GetGlobalObjectInfo(position);

            if(currentData == null) return true;
            if (currentData.AdditionalOccupiedCells.Length == 0)
                return global.Remove(position);
            global.Remove(position);
            for (byte i = 0; i < currentData.AdditionalOccupiedCells.Length; i++)
                global.Remove(position + currentData.AdditionalOccupiedCells[i]);
            return true;
        }
        public bool ContainsGlobal(Vector2Int position) => global.ContainsKey(position);
        private PoolManager PoolManagerBase;
        public void CreatePool(int PrefabID, GameObject prefab, int poolSize = 1) => PoolManagerBase.CreatePool(PrefabID, prefab, InstantiatePoolObject, (ushort)poolSize);
        public void Reuse(int PrefabID, Vector3 position) => PoolManagerBase.Reuse(PrefabID, position);
        public void Deactivate(int PrefabID) => PoolManagerBase.Deactivate(PrefabID);
        public ushort GetPoolSize(int PrefabID) => PoolManagerBase.GetPoolSize(PrefabID);
        public void IncreasePoolSize(int PrefabID) => PoolManagerBase.IncreasePoolSize(PrefabID);
        public void SetUp(ISetAble.Callback callback)
        {
            PoolManagerBase = new PoolManager(this.transform);

            for (byte i = 0; i < _info.Length; i++)
            {
                info[_info[i].PrefabID] = _info[i];

                Debug.Log(_info[i].PrefabID + "   " + _info[i].ObjectName);
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
        public bool isExist;
        public Vector3 realPosition;
        public ObjectInstInfo(Vector3 _realPosition, int _PrefabID = -1, int _amount = 1, InstanceType _objectType = InstanceType.Static, bool _isExist = true)
        {
            realPosition = _realPosition;
            amount = _amount;
            PrefabID = _PrefabID;
            objectType = _objectType;
            isExist = _isExist;
        }
    }
}