using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;

using TheRavine.Generator;
using TheRavine.ObjectControl;

namespace TheRavine.EntityControl
{
    public class EntitySystem : MonoBehaviour, ISetAble
    {
        public GameObject CreateMob(Vector2 position, GameObject prefab)
        {
            GameObject curMob = Instantiate(prefab, position, Quaternion.identity);
            IEntity entity = curMob.GetComponentInChildren<AEntityViewModel>().Entity;

            if(entity is AEntity aEntity)
            {
                aEntity.Init();
            }

            return curMob;
        }
        private List<AEntity> global;
        public void AddToGlobal(AEntity entity)
        {
            global.Add(entity);
            // logger.LogInfo(entity.GetEntityComponent<MainComponent>().GetEntityName() + " added to EntitySystem!");
        }
        [SerializeField] private EntityInfo[] _mobInfo;
        [SerializeField] private BoidsBehaviour boidsBehaviour;
        private Dictionary<int, EntityInfo> mobInfo;
        public EntityInfo GetMobInfo(int id) => mobInfo[id];
        private RavineLogger logger;
        private System.IDisposable playersSubscription;
        private ChunkFoodIndex foodIndex;
        private CameraInputController cameraInput;
        private bool brokenUp;
        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Register(this);
            brokenUp = false;
            RegisterFoodIndex().Forget();

            logger = ServiceLocator.GetService<RavineLogger>();
            logger.LogInfo("EntitySystem service is available now");
            global  = new List<AEntity>();
            mobInfo = new Dictionary<int, EntityInfo>(4);

            playersSubscription?.Dispose();
            playersSubscription = ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ =>
                {
                    SetUpBoids(ServiceLocator.Players.GetAllPlayersTransform());
                    SetUpCameraInput();
                    logger.LogInfo("EntitySystem get all players list");
                });

            for (int i = 0; i < _mobInfo.Length; i++) mobInfo[_mobInfo[i].Prefab.GetInstanceID()] = _mobInfo[i];

            callback?.Invoke();
        }
        private async UniTaskVoid RegisterFoodIndex()
        {
            var map     = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
            var objects = await ServiceLocator.WaitUntilServiceReady<ObjectSystem>();
            if (brokenUp) return;

            foodIndex = new ChunkFoodIndex(map, objects.infoRegistry);
            ServiceLocator.Register(foodIndex);
        }

        private void SetUpBoids(IReadOnlyList<Transform> players)
        {
            Transform playerTransform = players[0];
            if(boidsBehaviour != null) boidsBehaviour.StartBoids(playerTransform);
        }

        private void SetUpCameraInput()
        {
            if (cameraInput != null) return;
            if (!ServiceLocator.Services.TryGet(out ActionMapController actions)) return;

            var player = ServiceLocator.Players.GetAllPlayers()[0];
            cameraInput = new CameraInputController(
                actions.GetActionFrom("Gameplay", "Camera Scroll"),
                actions.GetActionFrom("Gameplay", "Camera Flip"),
                player.GetEntityComponent<EventBusComponent>().EventBus);
            ServiceLocator.Register(cameraInput);
            cameraInput.Start();
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
            brokenUp = true;
            if (foodIndex != null) ServiceLocator.Services.Unregister(foodIndex);
            foodIndex = null;
            cameraInput?.Stop();
            if (cameraInput != null) ServiceLocator.Services.Unregister(cameraInput);
            cameraInput = null;
            OnDestroy();
            callback?.Invoke();
        }

        public void OnDestroy()
        {
            playersSubscription?.Dispose();
            playersSubscription = null;
            global?.Clear();
        }
    }
}