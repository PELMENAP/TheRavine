using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

using TheRavine.ObjectControl;
using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class EntitySystem : MonoBehaviour, ISetAble
    {
        // private SkillFacade skillFacade;
        private List<IEntity> global = new List<IEntity>(2);
        public void AddToGlobal(IEntity entity) => global.Add(entity);
        [SerializeField] private EntityInfo[] _mobInfo;
        private Dictionary<int, EntityInfo> mobInfo = new Dictionary<int, EntityInfo>(4);
        public EntityInfo GetMobInfo(int id) => mobInfo[id];
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            // skillFacade = new SkillFacade();

            for (int i = 0; i < _mobInfo.Length; i++)
                mobInfo[_mobInfo[i].prefab.GetInstanceID()] = _mobInfo[i];

            callback?.Invoke();
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < global.Count; i++)
                global[i].UpdateEntityCycle();
        }

        public void BreakUp()
        {
            global.Clear();
        }
    }
}