using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Cysharp.Threading.Tasks;

namespace TheRavine.EntityControl
{
    public class EntitySystem : NetworkBehaviour, ISetAble
    {
        public GameObject CreateMob(Vector2 position, GameObject prefab)
        {
            GameObject curMob = Instantiate(prefab, position, Quaternion.identity);
            AEntity entity = curMob.GetComponentInChildren<AEntityViewModel>().Entity;
            
            // if(entity != null)
            // {
            //     entity.Init();
            // }

            return curMob;
        }
        // private SkillFacade skillFacade;
        private List<AEntity> global;
        public void AddToGlobal(AEntity entity) => global.Add(entity);
        [SerializeField] private EntityInfo[] _mobInfo;
        [SerializeField] private BoidsBehaviour boidsBehaviour;
        private Dictionary<int, EntityInfo> mobInfo;
        public EntityInfo GetMobInfo(int id) => mobInfo[id];
        private IRavineLogger logger;
        public void SetUp(ISetAble.Callback callback)
        {
            // skillFacade = new SkillFacade();
            logger = ServiceLocator.GetService<IRavineLogger>();
            logger.LogInfo("EntitySystem service is available now");
            global  = new List<AEntity>();
            mobInfo = new Dictionary<int, EntityInfo>(4);

            for (int i = 0; i < _mobInfo.Length; i++) mobInfo[_mobInfo[i].Prefab.GetInstanceID()] = _mobInfo[i];

            SetUpBoids().Forget();
            callback?.Invoke();
        }
        private async UniTaskVoid SetUpBoids()
        {
            await UniTask.Delay(1000);
            if(boidsBehaviour != null) boidsBehaviour.StartBoids(ServiceLocator.Players.GetFirstPlayer());
        }
        private void FixedUpdate()
        {
            if(global == null) return;
            for (int i = 0; i < global.Count; i++) global[i].UpdateEntityCycle();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            for (int i = 0; i < global.Count; i++) global[i].Dispose();
            if(boidsBehaviour != null) boidsBehaviour.DisableBoids();
            OnDestroy();
            callback?.Invoke();
        }

        public override void OnDestroy()
        {
            global?.Clear();
        }
    }
}