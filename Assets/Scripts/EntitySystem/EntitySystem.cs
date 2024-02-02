using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

using TheRavine.ObjectControl;

namespace TheRavine.EntityControl
{
    public class EntitySystem : MonoBehaviour, ISetAble
    {
        private IPoolManager<GameObject> EntityPoolManager;
        [SerializeField] private int count;

        private EntityInfo[] _info;
        private Dictionary<int, EntityInfo> info = new Dictionary<int, EntityInfo>(16);

        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            EntityPoolManager = new PoolManager(this.transform);
            for (int i = 0; i < _info.Length; i++)
            {
                // info[] = _info[i];
            }
            callback?.Invoke();
        }

        public void BreakUp()
        {

        }
    }
}