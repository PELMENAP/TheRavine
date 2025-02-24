using UnityEngine;
using Unity.Netcode;
using System;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class PlayerModelView : NetworkBehaviour, ISetAble
    {
        public PlayerEntity playerEntity { get; private set; }
        [SerializeField] private NetworkObject cameraPrefab;
        private Camera mainCamera;
        private ServiceLocator locator;

        public override void OnNetworkSpawn() 
        {

            locator = ServiceLocatorAccess.inst.serviceLocator;
            locator.RegisterPlayer<PlayerModelView>(this);
            locator.Register<PlayerModelView>(this);

            SetUp(null, locator);
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
            Camera[] cameras = Camera.allCameras;
            foreach (var cam in cameras)
            {
                if (cam != mainCamera && cam.GetComponent<NetworkObject>()?.OwnerClientId == ownerId)
                {
                    cam.gameObject.SetActive(false);
                }
            }
        }

        [SerializeField] private EntityInfo playerInfo;
        private CM cameraComponent;
        private ILogger logger;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            logger = locator.GetLogger();


            try
            {
                playerEntity = new PlayerEntity(playerInfo);
            }
            catch (Exception ex)
            {
                logger.LogError($"Player entity {NetworkManager.Singleton.LocalClientId} cannot be created: {ex.Message}");
            }

            try
            {
                playerEntity.OnActiveStateChanged += HandleStateChanged;
                AddComponentsToEntity();
                playerEntity.Init(OnViewUpdate, this.GetComponent<IEntityController>());
            }
            catch (Exception ex)
            {
                logger.LogError($"Player entity {NetworkManager.Singleton.LocalClientId} cannot be initialized: {ex.Message}");
            }
            finally
            {
                logger.LogInfo($"Player {NetworkManager.Singleton.LocalClientId} is set up");
            }

            callback?.Invoke();
        }
        private void AddComponentsToEntity()
        {
            playerEntity.AddComponentToEntity(new TransformComponent(this.transform, this.transform));
        }
        private void OnViewUpdate()
        {
            cameraComponent?.CameraUpdate();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            try
            {
                playerEntity.OnActiveStateChanged -= HandleStateChanged;
                playerEntity.Delete();
            }
            catch
            {
                logger.LogWarning("Player entity hadn't created");
            }
            
            cameraComponent?.BreakUp(callback);
        }

        private void HandleStateChanged()
        {
            if (playerEntity.IsActive)
                EnableView();
            else
                DisableView();
        }

        public void EnableView()
        {

        }
        public void DisableView()
        {

        }

        private void OnDisable() {
            BreakUp(null);
        }

        public override void OnDestroy()
        {
            BreakUp(null);
        }
    }
}