using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using R3;

namespace TheRavine.EntityControl
{
    public class EntitySystem : MonoBehaviour, ISetAble
    {
        public GameObject CreateMob(Vector2 position, GameObject prefab)
        {
            GameObject curMob = Instantiate(prefab, position, Quaternion.identity);
            AEntity entity = curMob.GetComponentInChildren<AEntityViewModel>().Entity;
            
            if(entity != null)
            {
                entity.Init();
            }

            return curMob;
        }
        private List<AEntity> global;
        public void AddToGlobal(AEntity entity)
        {
            global.Add(entity);
            logger.LogInfo(entity.GetEntityComponent<MainComponent>().GetEntityName() + " added to EntitySystem!");
        }
        [SerializeField] private EntityInfo[] _mobInfo;
        [SerializeField] private BoidsBehaviour boidsBehaviour;
        private Dictionary<int, EntityInfo> mobInfo;
        public EntityInfo GetMobInfo(int id) => mobInfo[id];
        private RavineLogger logger;
        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);
            
            logger = ServiceLocator.GetService<RavineLogger>();
            logger.LogInfo("EntitySystem service is available now");
            global  = new List<AEntity>();
            mobInfo = new Dictionary<int, EntityInfo>(4);

            ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ =>
                {
                    SetUpBoids(ServiceLocator.Players.GetAllPlayersTransform());
                    logger.LogInfo("EntitySystem get all players list");
                });

            for (int i = 0; i < _mobInfo.Length; i++) mobInfo[_mobInfo[i].Prefab.GetInstanceID()] = _mobInfo[i];

            callback?.Invoke();
        }
        private void SetUpBoids(IReadOnlyList<Transform> players)
        {
            Transform playerTransform = players[0];
            if(boidsBehaviour != null) boidsBehaviour.StartBoids(playerTransform);
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

        public void OnDestroy()
        {
            global?.Clear();
        }
    }
}