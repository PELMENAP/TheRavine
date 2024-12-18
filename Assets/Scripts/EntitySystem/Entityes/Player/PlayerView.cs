using UnityEngine;
using Unity.Netcode;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class PlayerView : NetworkBehaviour, ISetAble
    {
        public PlayerEntity playerEntity { get; private set; }
        [SerializeField] private NetworkObject cameraPrefab;
        private Camera mainCamera;
        private ServiceLocator locator;

        public override void OnNetworkSpawn() 
        {

            locator = ServiceLocatorAccess.inst.serviceLocator;
            locator.RegisterPlayer<PlayerView>(this);
            locator.Register<PlayerView>(this);

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
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            playerEntity = new PlayerEntity(); // need factory 
            playerEntity.OnActiveStateChanged += HandleStateChanged;
            
            AddComponentsToEntity();
            
            playerEntity.SetUpEntityData(playerInfo, this.GetComponent<IEntityController>());
            playerEntity.Init(OnViewUpdate);

            callback?.Invoke();
            Debug.Log("player is set up");
        }
        private void AddComponentsToEntity()
        {
            playerEntity.AddComponentToEntity(new TransformComponent(this.transform, this.transform));
        }
        private void OnViewUpdate()
        {
            cameraComponent?.CameraUpdate();
        }
        private void AimSkills()
        {
            // if (Input.GetKey("space") && Input.GetMouseButton(1))
            // {
            //     Vector3 playerPos = entityTrans.position;
            //     ui.UseSkill("Rush", aim, ref playerPos);
            //     entityTrans.position = playerPos;
            // }
        }

        private void ReloadSkills()
        {
        }

        public void Priking()
        {
            // StartCoroutine(Prick());
        }

        // private IEnumerator Prick()
        // {
        //     // moving = false;
        //     // animator.SetBool("isPrick", true);
        //     yield return new WaitForSeconds(1);
        //     // animator.SetBool("isPrick", false);
        //     // moving = true;
        // }

        public void BreakUp(ISetAble.Callback callback)
        {
            if(playerEntity != null)
                playerEntity.OnActiveStateChanged -= HandleStateChanged;
            playerEntity.Delete();
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