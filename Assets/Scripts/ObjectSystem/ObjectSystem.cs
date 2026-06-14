using UnityEngine;

namespace TheRavine.ObjectControl
{
    public class ObjectSystem : MonoBehaviour, ISetAble
    {
        public ObjectInfoRegistry infoRegistry;

        private PoolManager poolManager;
        public GameObject InstantiatePoolObject(Vector3 position, GameObject prefab) =>
            Instantiate(prefab, position, Quaternion.identity);
        public ObjectInfo GetInfo(int prefabID) => infoRegistry.Get(prefabID);

        public void CreatePool(int prefabID, GameObject prefab, int poolSize = 1) =>
            poolManager.CreatePool(prefabID, prefab, InstantiatePoolObject, poolSize);

        public void Reuse(int prefabID, Vector3 position) =>
            poolManager.Reuse(prefabID, position);

        public void Deactivate(int prefabID) =>
            poolManager.Deactivate(prefabID);

        public int GetPoolSize(int prefabID) =>
            poolManager.GetPoolSize(prefabID);

        public void IncreasePoolSize(int prefabID) =>
            poolManager.IncreasePoolSize(prefabID);

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);

            infoRegistry.RebuildDictionary();
            poolManager = new PoolManager(transform);

            var infos = infoRegistry.objectInfos;
            for (int i = 0; i < infos.Count; i++)
                CreatePool(infos[i].PrefabID, infos[i].ObjectPrefab, infos[i].InitialPoolSize);

            callback?.Invoke();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            OnDisable();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            infoRegistry?.Clear();
            poolManager?.Dispose();
        }
    }
}