using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class EntitySystem : NetworkBehaviour, ISetAble
    {
        // private SkillFacade skillFacade;
        private List<AEntity> global;
        public void AddToGlobal(AEntity entity) => global.Add(entity);
        [SerializeField] private EntityInfo[] _mobInfo;
        [SerializeField] private BoidsBehaviour boidsBehaviour;
        private Dictionary<int, EntityInfo> mobInfo;
        public EntityInfo GetMobInfo(int id) => mobInfo[id];
        private ILogger logger;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            // skillFacade = new SkillFacade();
            logger = locator.GetLogger();
            Debug.Log("entsys is start");
            global  = new List<AEntity>();
            mobInfo = new Dictionary<int, EntityInfo>(4);
            if(boidsBehaviour != null) boidsBehaviour.StartBoids().Forget();

            for (int i = 0; i < _mobInfo.Length; i++) mobInfo[_mobInfo[i].prefab.GetInstanceID()] = _mobInfo[i];

            callback?.Invoke();
        }

        private void FixedUpdate()
        {
            if(global == null) return;
            for (int i = 0; i < global.Count; i++) global[i].UpdateEntityCycle();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            for (int i = 0; i < global.Count; i++) global[i].Delete();
            if(boidsBehaviour != null) boidsBehaviour.DisableBoids();
            OnDestroy();
            callback?.Invoke();
        }

        public override void OnDestroy()
        {
            global.Clear();
        }
    }
}