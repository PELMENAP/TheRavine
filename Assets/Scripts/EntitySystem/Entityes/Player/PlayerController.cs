using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Cysharp.Threading.Tasks;

using System;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.Events;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float movementMinimum, sendInterval = 0.1f;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Transform crosshair, playerMark;
        [SerializeField] private PlayerAnimator playerAnimator;
        private Rigidbody playerRigidbody;
        private Vector2 movementDirection;
        private float timeSinceLastSend;
        public float currentSpeed = 0f;
        private bool isAimMode;
        private bool act = true;
        private bool isAccurance;
        private bool isHolding = false;

        private IController currentController;
        private IRavineLogger logger;
        private MovementComponent movementComponent;
        private EventBus entityEventBus;
        private AimComponent aimComponent;
        private GlobalSettings globalSettings;
        private AEntity playerEntity;
        private readonly DoubleTapDetector forwardTap = new();
        public void SetInitialValues(AEntity entity, IRavineLogger logger)
        {
            playerEntity = entity;
            globalSettings = ServiceLocator.GetService<SettingsMediator>().Global.CurrentValue;
            this.logger = logger;
            

            playerAnimator.SetUpAsync().Forget();

            GetPlayerComponents();
            DelayedInit().Forget();

            Camera camera = playerEntity.GetEntityComponent<CameraComponent>().GetCamera();

            currentController = globalSettings.controlType switch
            {
                ControlType.Personal => new PCController(Movement, camera, transform, aimComponent.CrosshairDistance),
                ControlType.Mobile => new JoistickController(joystick),
                _ => null
            };

            if(currentController is null) logger.LogError("Control type not defined");

            Raise.action.performed += AimRaise;
            LeftClick.action.performed += AimPlace;

            RightClick.action.started += OnActionStarted;
            RightClick.action.canceled += OnActionCanceled;
        }

        private async UniTaskVoid DelayedInit()
        {
            Vector2 spawnSpread = Extension.GetRandomPointAround(this.transform.position, 10);
            transform.position = new Vector3(spawnSpread.x, 200, spawnSpread.y);

            playerRigidbody.useGravity = false;
            await UniTask.Delay(5000);
            playerRigidbody.useGravity = true;

            spawnSpread = Extension.GetRandomPointAround(this.transform.position, 10);
            transform.position = new Vector3(spawnSpread.x, 200, spawnSpread.y);
        }

        private void GetPlayerComponents()
        {
            playerRigidbody = GetComponent<Rigidbody>();
            
            entityEventBus = playerEntity.GetEntityComponent<EventBusComponent>().EventBus;
            movementComponent = playerEntity.GetEntityComponent<MovementComponent>();
            aimComponent = playerEntity.GetEntityComponent<AimComponent>();
            InitStatePattern(playerEntity.GetEntityComponent<StatePatternComponent>());
        }

        private void InitStatePattern(StatePatternComponent component)
        {
            Action actions = Move;
            actions += Aim;
            component.AddBehaviour(typeof(PlayerBehaviourIdle), new PlayerBehaviourIdle (this, actions, logger));
            
            actions = Aim;
            component.AddBehaviour(typeof(PlayerBehaviourSit), new PlayerBehaviourSit(this, actions));
        }

        public void SetZeroValues()
        {
            // movementDirection = new Vector2(0, 0);
            playerAnimator.Animate(movementDirection, 0);
        }
        public void EnableComponents()
        {
            currentController.EnableView();
        }
        public void DisableComponents()
        {
            currentController.DisableView();
        }
        public void Move()
        {
            if (isAimMode || !IsOwner) return;

            movementDirection = currentController.GetMove();

            float movementMagnitute = movementDirection.magnitude;

            CheckBust(movementMagnitute);

            float movementSpeed = Mathf.Clamp(movementMagnitute, 0f, 1f);

            if (movementSpeed < movementMinimum) movementDirection = Vector2.zero;
            else MoveMark();

            timeSinceLastSend += Time.deltaTime;
            if (timeSinceLastSend >= sendInterval)
            {
                MoveServerRpc(movementDirection.normalized, movementSpeed);
                timeSinceLastSend = 0f;
            }

            playerAnimator.Animate(movementDirection, movementSpeed);
        }

        private void CheckBust(float movementMagnitute)
        {
            forwardTap.Update(movementMagnitute > 0.9f);
            if (forwardTap.IsBoostActive && currentSpeed < 1f)
            {
                currentSpeed = movementComponent.BaseSpeed / 2;
            }
        }
        [ServerRpc]
        private void MoveServerRpc(Vector2 direction, float inputSpeed)
        {
            if (inputSpeed <= 0.01f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, movementComponent.Deceleration * Time.deltaTime);
            }

            float targetSpeed = inputSpeed * movementComponent.BaseSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, movementComponent.Acceleration * Time.deltaTime);
            playerRigidbody.linearVelocity = new(direction.x * currentSpeed, playerRigidbody.linearVelocity.y, direction.y * currentSpeed);


            UpdateClientPositionClientRpc(playerRigidbody.position, playerRigidbody.linearVelocity);
        }

        [ClientRpc]
        private void UpdateClientPositionClientRpc(Vector2 position, Vector2 velocity)
        {
            if (IsOwner) return;

            playerRigidbody.position = position;
            playerRigidbody.linearVelocity = velocity;
        }

        private readonly Vector3 Offset = new(0, 0, 100);
        private void MoveMark()
        {
            playerMark.position = transform.position + Offset;
            float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90;
            playerMark.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void OnActionStarted(InputAction.CallbackContext context)
        {
            isHolding = true;
        }

        private void OnActionCanceled(InputAction.CallbackContext context)
        {
            isHolding = false;
        }
        public void Aim()
        {
            if (!isHolding)
            {
                crosshair.gameObject.SetActive(false);
                isAccurance = false;
                return;
            } 

            Vector2 aim = currentController.GetAim();

            Vector2 aimNorm = aim.normalized;
            float magnitude = aim.magnitude;
            
            SetAimAddition(magnitude, aimNorm);

            if (magnitude < aimComponent.CrosshairDistance) crosshair.localPosition = new Vector3(aim.x, 0,  aim.y);
            else 
            {
                crosshair.localPosition = new Vector3(aimNorm.x * aimComponent.CrosshairDistance, 0, aimNorm.y * aimComponent.CrosshairDistance);
            }
            crosshair.gameObject.SetActive(true);
            isAccurance = true;
        }

        private void SetAimAddition(float magnitude, Vector3 aimNorm){
            
            float factMouseFactor = 1f;

            switch (globalSettings.controlType)
            {
                case ControlType.Personal:
                    factMouseFactor = 1f;
                    break;
                case ControlType.Mobile:
                    if(isAimMode) factMouseFactor = 0.2f;
                    break;
            }

            if (magnitude > aimComponent.MaxCrosshairDistance * factMouseFactor) aimNorm *= aimComponent.MaxCrosshairDistance;
            else if (magnitude < aimComponent.CrosshairDistance * factMouseFactor + 1) aimNorm = Vector2.zero;

            entityEventBus.Invoke(playerEntity, new AimAddition {Position = aimNorm});
        }
        
        public void ChangeAimMode()
        {
            isAimMode = !isAimMode;
        }

        public void AimRaiseMobile()
        {
            AimRaise(new InputAction.CallbackContext());
        }

        private void AimRaise(InputAction.CallbackContext obj)
        {
            if (!isAccurance)
            {
                int currentX = Mathf.RoundToInt(transform.position.x);
                int currentZ = Mathf.RoundToInt(transform.position.z);
                for (int xOffset = -aimComponent.PickDistance; xOffset <= aimComponent.PickDistance; xOffset++)
                    for (int zOffset = -aimComponent.PickDistance; zOffset <= aimComponent.PickDistance; zOffset++)
                        entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = new Vector2Int(currentX + xOffset, currentZ + zOffset) });
            }
            else entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = Extension.RoundVector2D(crosshair.position) });
        }

        public void AimPlaceMobile()
        {
            AimPlace(new InputAction.CallbackContext());
        }

        private void AimPlace(InputAction.CallbackContext obj)
        {
            if (crosshair.gameObject.activeSelf)
            {
                if (act) Placing().Forget();
            }
        }

        private async UniTaskVoid Placing()
        {
            // try
            // {
                act = false;
                entityEventBus.Invoke(playerEntity, new PlaceEvent { Position = Extension.RoundVector2D(crosshair.position) } );
                await UniTask.Delay(placeObjectDelay);
                act = true;
            // }
            // catch (Exception ex)
            // {
            //     logger?.LogError($"Error in In(): {ex.Message}");
            // }
        }
        public void Delete()
        {
            currentController.MeetEnds();
            Raise.action.performed -= AimRaise;
            LeftClick.action.performed -= AimPlace;

            RightClick.action.started -= OnActionStarted;
            RightClick.action.canceled -= OnActionCanceled;
        }
    }
}