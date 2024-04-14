using System.Collections.Generic;
using UnityEngine;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class EntitySystem : MonoBehaviour, ISetAble
    {
        // private SkillFacade skillFacade;
        private List<AEntity> global = new List<AEntity>(2);
        public void AddToGlobal(AEntity entity) => global.Add(entity);
        [SerializeField] private EntityInfo[] _mobInfo;
        [SerializeField] private BoidsBehaviour boidsBehaviour;
        private Dictionary<int, EntityInfo> mobInfo = new Dictionary<int, EntityInfo>(4);
        public EntityInfo GetMobInfo(int id) => mobInfo[id];
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            // skillFacade = new SkillFacade();
            if(boidsBehaviour != null) boidsBehaviour.StartBoids().Forget();

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
            for (int i = 0; i < global.Count; i++)
                global[i].Death();
            OnDestroy();
        }

        private void OnDestroy()
        {
            global.Clear();
        }
    }
}