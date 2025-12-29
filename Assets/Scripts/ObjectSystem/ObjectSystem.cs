using System.Collections.Generic;
using UnityEngine;

namespace TheRavine.ObjectControl
{
    public class ObjectSystem : MonoBehaviour, ISetAble
    {
        private const int InitialGlobalCapacity = 512;
        public GameObject InstantiatePoolObject(Vector3 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        [SerializeField] public ObjectInfoRegistry infoRegistry;
        private readonly Dictionary<Vector2Int, ObjectInstInfo> global = new (InitialGlobalCapacity);
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
                return infoRegistry.Get(instInfo.PrefabID);
            else
                return null;
        }
        public bool TryAddToGlobal(Vector2Int position, Vector3 realPosition, int _PrefabID, int _amount, InstanceType _objectType)
        {
            if (global.ContainsKey(position))
                if (global[position].PrefabID == _PrefabID && global[position].Type == InstanceType.Interactable)
                {
                    global[position] = new ObjectInstInfo(realPosition, _PrefabID, global[position].Amount + _amount, InstanceType.Interactable); ;
                    return true;
                }
            ObjectInfo currentData = infoRegistry.Get(_PrefabID);
            ObjectInstInfo objectInfo = new(realPosition, _PrefabID, _amount, _objectType);

            if(currentData == null)
            {
                return false;
            }

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
        public void CreatePool(int PrefabID, GameObject prefab, int poolSize = 1) => PoolManagerBase.CreatePool(PrefabID, prefab, InstantiatePoolObject, poolSize);
        public void Reuse(int PrefabID, Vector3 position) => PoolManagerBase.Reuse(PrefabID, position);
        public void Deactivate(int PrefabID) => PoolManagerBase.Deactivate(PrefabID);
        public int GetPoolSize(int PrefabID) => PoolManagerBase.GetPoolSize(PrefabID);
        public void IncreasePoolSize(int PrefabID) => PoolManagerBase.IncreasePoolSize(PrefabID);
        public void SetUp(ISetAble.Callback callback)
        {
            PoolManagerBase = new PoolManager(transform);

            for(int i = 0; i < infoRegistry.objectInfos.Count; i++)
            {
                CreatePool(infoRegistry.objectInfos[i].PrefabID, infoRegistry.objectInfos[i].ObjectPrefab, infoRegistry.objectInfos[i].InitialPoolSize);
            }

            callback?.Invoke();
        }
        public void BreakUp(ISetAble.Callback callback)
        {
            infoRegistry.Clear();
            global.Clear();
            callback?.Invoke();
        }

    }


    public readonly struct ObjectInstInfo
    {
        public readonly int PrefabID;
        public readonly InstanceType Type;
        public readonly Vector3 Position;
        public readonly int Amount;
        public readonly bool Exists;

        public ObjectInstInfo(
            Vector3 pos, int prefab, int amount,
            InstanceType type, bool exists = true)
        {
            Position = pos;
            PrefabID = prefab;
            Amount = amount;
            Type = type;
            Exists = exists;
        }
    }
}