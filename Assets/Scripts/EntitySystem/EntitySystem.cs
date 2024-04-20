using System.Collections.Generic;
using UnityEngine;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class EntitySystem : MonoBehaviour, ISetAble
    {
        // private SkillFacade skillFacade;
        private List<AEntity> global;
        public void AddToGlobal(AEntity entity) => global.Add(entity);
        [SerializeField] private EntityInfo[] _mobInfo;
        [SerializeField] private BoidsBehaviour boidsBehaviour;
        private Dictionary<int, EntityInfo> mobInfo;
        public EntityInfo GetMobInfo(int id) => mobInfo[id];
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            // skillFacade = new SkillFacade();
            global  = new List<AEntity>(2);
            mobInfo = new Dictionary<int, EntityInfo>(4);
            if(boidsBehaviour != null) boidsBehaviour.StartBoids().Forget();

            for (int i = 0; i < _mobInfo.Length; i++) mobInfo[_mobInfo[i].prefab.GetInstanceID()] = _mobInfo[i];

            callback?.Invoke();
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < global.Count; i++) global[i].UpdateEntityCycle();
        }

        public void BreakUp()
        {
            for (int i = 0; i < global.Count; i++) global[i].Death();
            if(boidsBehaviour != null) boidsBehaviour.DisableBoids();
            OnDestroy();
        }

        private void OnDestroy()
        {
            global.Clear();
        }
    }
}