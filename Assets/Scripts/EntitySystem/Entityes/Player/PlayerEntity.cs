using UnityEngine;
using Unity.Netcode;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class PlayerEntity : AEntity, ISetAble
    {
        [SerializeField] private NetworkObject cameraPrefab;
        private Camera mainCamera;
        private ServiceLocator locator;

        public override void OnNetworkSpawn() 
        {

            locator = ServiceLocatorAccess.inst.serviceLocator;
            SetUp(null, locator);
            if (IsClient && IsOwner) 
            {
                RequestCameraServerRpc(NetworkManager.Singleton.LocalClientId);
                locator.GetService<EntitySystem>().AddToGlobal(this);
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
                cameraComponent.SetPlayerEntity(this);
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
        private IEntityControllable controller;
        private StatePatternComponent statePatternComponent;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            base.Birth();
            controller = this.GetComponent<IEntityControllable>();
            statePatternComponent = new StatePatternComponent();
            base.AddComponentToEntity(statePatternComponent);
            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            Init();
            callback?.Invoke();
            Debug.Log("player is set up");
        }

        public override void Init()
        {
            SetUpEntityData(playerInfo);
            controller.SetInitialValues(this);
            SetBehaviourIdle();
        }

        public override void SetUpEntityData(EntityInfo entityInfo)
        {
            // _entityGameData = new EntityGameData(_entityInfo);
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.prefab.GetInstanceID(), new EntityStats(entityInfo.statsInfo)));
            base.AddComponentToEntity(new MovementComponent(new EntityMovementBaseStats(entityInfo.movementStatsInfo)));
            base.AddComponentToEntity(new AimComponent(new EntityAimBaseStats(entityInfo.aimStatsInfo)));
        }
        public override Vector2 GetEntityPosition() => new(this.transform.position.x, this.transform.position.y);
        public override Vector2 GetEntityVelocity()
        {
            return new Vector2();
        }
        public override Transform GetModelTransform()
        {
            return this.transform;
        }
        public override void UpdateEntityCycle()
        {
            if (!IsAlife()) return;
            if (statePatternComponent.behaviourCurrent == null) return;
            statePatternComponent.behaviourCurrent.Update();
            cameraComponent?.CameraUpdate();
        }

        public void SetBehaviourIdle()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourIdle>());
            controller.EnableComponents();
        }

        public void SetBehaviourDialog()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourDialogue>());
            controller.SetZeroValues();
            controller.DisableComponents();
        }

        public void SetBehaviourSit()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourSit>());
            controller.SetZeroValues();
            controller.DisableComponents();
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

        public void MoveTo(Vector2 newPosition)
        {
            this.transform.position = newPosition;
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            Death();
            controller.Delete();
            cameraComponent?.BreakUp(callback);
        }

        public override void EnableView()
        {

        }
        public override void DisableView()
        {

        }

        private void OnDisable() {
            BreakUp(null);
        }

        public override void OnDestroy()
        {
            statePatternComponent.Dispose();
            BreakUp(null);
        }
    }
}