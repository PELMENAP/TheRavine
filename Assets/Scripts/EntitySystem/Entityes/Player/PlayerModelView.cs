using Unity.Netcode;
using UnityEngine;
using System;

using Cysharp.Threading.Tasks;
using TheRavine.Base;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerModelView : AEntityViewModel
    {
        public PlayerEntity PlayerEntity => (PlayerEntity)Entity;
        [SerializeField] private NetworkObject cameraPrefab;
        [SerializeField] private EntityInfo playerInfo;

        [SerializeField] private CameraMovingConfig cameraMove;
        private CameraComponent cameraComponent;
        private RavineLogger logger;
        public override async void OnNetworkSpawn()
        {
            logger = ServiceLocator.GetService<RavineLogger>();

            await CreatePlayerEntity();
            SetupLocator();
            await SetupNetworking();

            PlayerEntity.SetUp();
            PlayerEntity.AddWorldComponents(ServiceLocator.GetService<WorldRegistry>());
        }

        private void SetupLocator()
        {
            try
            {
                if(ServiceLocator.Players.RegisterPlayer(PlayerEntity))
                {
                    logger.LogInfo($"Player entity {NetworkManager.Singleton.LocalClientId} is registered in service locator");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Player entity {NetworkManager.Singleton.LocalClientId} cannot setup locator: {ex.Message}");
            }
        }

        private async UniTask CreatePlayerEntity()
        {
            base.Initialize(new PlayerEntity(logger));
            PlayerEntity.AddComponentsToEntity(playerInfo, this, NetworkManager.Singleton.LocalClientId);

            PlayerEntity.Init();

            await UniTask.CompletedTask;
        }

        private async UniTask SetupNetworking()
        {
            try
            {
                if (!IsClient || !IsOwner) return;
                
                RequestCameraServerRpc(NetworkManager.Singleton.LocalClientId);
                EntitySystem entitySystem = await WaitUntilServiceReady<EntitySystem>();
                entitySystem.AddToGlobal(PlayerEntity);

                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError($"Player entity {NetworkManager.Singleton.LocalClientId} cannot setup networking: {ex.Message}");
            }
        }
        private async UniTask<T> WaitUntilServiceReady<T>() where T : class
        {
            T service;
            while (!ServiceLocator.Services.TryGet(out service))
            {
                await UniTask.Yield();
            }
            return service;
        }
        [ServerRpc]
        private void RequestCameraServerRpc(ulong clientId)
        {
            NetworkObject cameraObject = Instantiate(cameraPrefab);
            cameraObject.SpawnWithOwnership(clientId);
            NotifyCameraCreatedClientRpc(cameraObject.NetworkObjectId, clientId);
        }

        [ClientRpc]
        private void NotifyCameraCreatedClientRpc(ulong cameraObjectId, ulong clientId)
        {
            var cameraObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[cameraObjectId];
            Camera camera = cameraObject.GetComponent<Camera>();
            cameraComponent = new CameraComponent();

            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                cameraComponent.SetUp(PlayerEntity, camera, cameraObject.transform, cameraMove);
                PlayerEntity.AddComponentToEntity(cameraComponent);
            }
            DisableOtherCameras(clientId, camera);
        }

        private void DisableOtherCameras(ulong ownerId, Camera mainCamera)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam == mainCamera) continue;

                var netObj = cam.GetComponent<NetworkObject>();
                if (netObj != null && netObj.OwnerClientId == ownerId)
                {
                    cam.gameObject.SetActive(false);
                }
            }
        }

        protected override void OnViewUpdate()
        {
            cameraComponent?.CameraUpdate();
        }
        protected override void OnViewEnable()
        {
            
        }
        protected override void OnViewDisable()
        {
            
        }
        public override void OnDestroy() 
        {
            cameraComponent?.Dispose();
            PlayerEntity.Dispose();
        }
    }
}