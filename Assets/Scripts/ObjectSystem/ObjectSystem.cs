using UnityEngine;

namespace TheRavine.ObjectControl
{
    public class ObjectSystem : MonoBehaviour, ISetAble
    {
        public ObjectInfoRegistry infoRegistry;

        private PoolManager poolManager;

        public ObjectInfo GetInfo(int id) => infoRegistry.Get(id);

        public bool TryGetIdByPrefab(GameObject prefab, out int id) => infoRegistry.TryGetIdByPrefab(prefab, out id);

        public PoolManager.Pool RegisterPool(int id, GameObject prefab, int prewarm = 0) =>
            poolManager.Register(id, prefab, prewarm);

        public bool TryGetPool(int id, out PoolManager.Pool pool) => poolManager.TryGetPool(id, out pool);

        public PooledObject Get(int id, Vector3 position) => poolManager.Get(id, position);

        public void Release(int id, PooledObject obj) => poolManager.Release(id, obj);

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);

            infoRegistry.Rebuild();
            poolManager = new PoolManager(transform);

            for (int i = 0; i < infoRegistry.Count; i++)
            {
                ObjectInfo info = infoRegistry.GetDense(i);
                RegisterPool(info.Id, info.ObjectPrefab, info.InitialPoolSize);
            }

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
