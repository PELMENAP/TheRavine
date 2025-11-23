using Unity.Netcode;
using UnityEngine;
using System;

using Cysharp.Threading.Tasks;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(IEntityController))]
    public class PlayerModelView : AEntityViewModel
    {
        public PlayerEntity PlayerEntity => (PlayerEntity)Entity;
        [SerializeField] private NetworkObject cameraPrefab;
        [SerializeField] private EntityInfo playerInfo;
        private Camera mainCamera;
        private CM cameraComponent;
        private IRavineLogger logger;
        public override async void OnNetworkSpawn()
        {
            await SetupLocator();
            await CreatePlayerEntity();
            await SetupNetworking();

            PlayerEntity.Init();
            logger.LogInfo($"Player entity {NetworkManager.Singleton.LocalClientId} is set up");
        }

        private async UniTask SetupLocator()
        {
            try
            {
                ServiceLocator.Services.Register(this);
                ServiceLocator.Players.RegisterPlayer(this.transform);

                await UniTask.Delay(3000);

                logger = ServiceLocator.GetService<IRavineLogger>();
            }
            catch (Exception ex)
            {
                logger.LogError($"Player entity {NetworkManager.Singleton.LocalClientId} cannot setup locator: {ex.Message}");
            }
        }

        private async UniTask CreatePlayerEntity()
        {
            base.Initialize(new PlayerEntity(logger));
            PlayerEntity.AddComponentsToEntity(playerInfo, this, GetComponent<IEntityController>());

            await UniTask.CompletedTask;
        }

        private async UniTask SetupNetworking()
        {
            try
            {
                if (IsClient && IsOwner)
                {
                    RequestCameraServerRpc(NetworkManager.Singleton.LocalClientId);
                    ServiceLocator.GetService<EntitySystem>().AddToGlobal(PlayerEntity);
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
            cameraComponent = cameraObject.GetComponent<CM>();

            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                cameraComponent.SetPlayerEntity(PlayerEntity);
                cameraComponent.SetUp(null);
                mainCamera = cameraObject.GetComponent<Camera>();
            }
            DisableOtherCameras(clientId);
        }

        private void DisableOtherCameras(ulong ownerId)
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
            cameraComponent?.BreakUp(null);
        }
    }
}