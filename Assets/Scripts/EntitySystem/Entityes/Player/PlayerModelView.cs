using Unity.Netcode;
using UnityEngine;
using System;

using Cysharp.Threading.Tasks;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerModelView : AEntityViewModel
    {
        public PlayerEntity PlayerEntity => (PlayerEntity)Entity;
        [SerializeField] private NetworkObject cameraPrefab;
        [SerializeField] private EntityInfo playerInfo;
        private CameraComponent cameraComponent;
        private IRavineLogger logger;
        public override async void OnNetworkSpawn()
        {
            logger = ServiceLocator.GetService<IRavineLogger>();

            await CreatePlayerEntity();
            SetupLocator();
            await SetupNetworking();

            PlayerEntity.Init();
        }

        private void SetupLocator()
        {
            try
            {
                ServiceLocator.Services.Register(this);

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

            await UniTask.CompletedTask;
        }

        private async UniTask SetupNetworking()
        {
            try
            {
                if (IsClient && IsOwner)
                {
                    RequestCameraServerRpc(NetworkManager.Singleton.LocalClientId);
                    
                    while(true)
                    {
                        if(ServiceLocator.Services.TryGet(out EntitySystem entitySystem))
                        {
                            entitySystem.AddToGlobal(PlayerEntity);
                            break;
                        }
                        await UniTask.Delay(1000);
                    }
                }
                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError($"Player entity {NetworkManager.Singleton.LocalClientId} cannot setup networking: {ex.Message}");
            }
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
                cameraComponent.SetUp(PlayerEntity, camera, cameraObject.transform);
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