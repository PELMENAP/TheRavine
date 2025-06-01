using Unity.Netcode;
using UnityEngine;
using R3;
using System;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class PlayerModelView : AEntityViewModel
    {
        public PlayerEntity playerEntity => (PlayerEntity)Entity;
        [SerializeField] private NetworkObject cameraPrefab;
        [SerializeField] private EntityInfo playerInfo;
        private Camera mainCamera;
        private ServiceLocator locator;
        private CM cameraComponent;
        private ILogger logger;
        protected override void OnInitialize()
        {
            // doing something specific on view
        }
        public override void OnNetworkSpawn()
        {
            try
            {
                SetupLocator();
                CreatePlayerEntity();
                SetupNetworking();
                logger.LogInfo($"Player entity {NetworkManager.Singleton.LocalClientId} is set up");
            }
            catch (Exception ex)
            {
                logger.LogError($"Player entity {NetworkManager.Singleton.LocalClientId} cannot be created: {ex.Message}");
            }

            playerEntity.Init();
        }

        private void SetupLocator()
        {
            locator = ServiceLocatorAccess.inst.serviceLocator;
            locator.RegisterPlayer<PlayerModelView>(this);
            locator.Register<PlayerModelView>(this);
            logger = locator.GetLogger();
        }

        private void CreatePlayerEntity()
        {
            Initialize(new PlayerEntity(GetComponent<IEntityController>(), logger));
            playerEntity.AddComponentsToEntity(playerInfo, this);
        }

        private void SetupNetworking()
        {
            if (IsClient && IsOwner)
            {
                RequestCameraServerRpc(NetworkManager.Singleton.LocalClientId);
                locator.GetService<EntitySystem>().AddToGlobal(playerEntity);
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
                cameraComponent.SetPlayerEntity(playerEntity);
                cameraComponent.SetUp(null, locator);
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