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
        private const float SpawnHeightY = 200f;

        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float movementMinimum, sendInterval = 0.1f;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick, Jump;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Transform crosshair, playerMark;
        [SerializeField] private PlayerAnimator playerAnimator;

        [Header("Jump")]
        [SerializeField] private float baseJumpForce = 7f;
        [SerializeField] private AnimationCurve jumpForceCurve;
        [SerializeField] private GroundChecker groundChecker;

        private Rigidbody playerRigidbody;
        private Vector2 movementDirection;
        private float timeSinceLastSend;
        public float currentSpeed { get; private set; }
        private bool isAimMode;
        private bool canPlace = true;
        private bool isAccurance;
        private bool isHolding;
        private bool isFlip;

        private IController currentController;
        private RavineLogger logger;
        private MovementComponent movementComponent;
        private EventBus entityEventBus;
        private AimComponent aimComponent;
        private GlobalSettings globalSettings;
        private AEntity playerEntity;

        private readonly DoubleTapDetector forwardTap = new();
        private readonly JumpChargeDetector jumpCharge = new(maxChargeTime: 1f);
        private IDisposable unsubscribe;

        public void SetInitialValues(AEntity entity, RavineLogger logger)
        {
            playerEntity = entity;
            globalSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
            this.logger = logger;

            WorldRegistry worldRegistry = ServiceLocator.GetService<WorldRegistry>();

            playerAnimator.SetUpAsync().Forget();
            GetPlayerComponents();
            DelayedInit(worldRegistry).Forget();

            unsubscribe = ServiceLocator.GetService<AutosaveCoordinator>().SubscribeBeforeSave(() =>
            {
                SavePlayerPosition(worldRegistry);
            });

            Raise.action.performed += AimRaise;
            LeftClick.action.performed += AimPlace;

            RightClick.action.started += OnActionStarted;
            RightClick.action.canceled += OnActionCanceled;

            Jump.action.started += OnJumpStarted;
            Jump.action.canceled += OnJumpCanceled;
        }

        public void SetUp()
        {
            Camera camera = playerEntity.GetEntityComponent<CameraComponent>().GetCamera();

            currentController = globalSettings.controlType switch
            {
                ControlType.Personal => new PCController(Movement, camera, transform, aimComponent.CrosshairDistance),
                ControlType.Mobile => new JoistickController(joystick),
                _ => null
            };
        }

        private void SavePlayerPosition(WorldRegistry worldRegistry)
        {
            worldRegistry.UpdateState(s =>
            {
                s.playerPosition = transform.position;
            });
        }

        private async UniTaskVoid DelayedInit(WorldRegistry worldRegistry)
        {
            playerRigidbody.useGravity = false;

            WorldState worldState = worldRegistry.GetCurrentState();

            if (!worldState.playerPosition.IsNull())
            {
                transform.position = worldState.playerPosition.ToSpawnPoint();
            }
            else
            {
                Vector2 spawnPoint = Extension.GetRandomPointAround(transform.position, 10);
                transform.position = new Vector3(spawnPoint.x, SpawnHeightY, spawnPoint.y);
            }

            await UniTask.Delay(5000);
            playerRigidbody.useGravity = true;
        }

        private void GetPlayerComponents()
        {
            playerRigidbody = GetComponent<Rigidbody>();

            entityEventBus = playerEntity.GetEntityComponent<EventBusComponent>().EventBus;
            movementComponent = playerEntity.GetEntityComponent<MovementComponent>();
            aimComponent = playerEntity.GetEntityComponent<AimComponent>();
            InitStatePattern(playerEntity.GetEntityComponent<StatePatternComponent>());

            playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<CameraPlace>(CameraPlaceHandleEvent);
        }

        private void InitStatePattern(StatePatternComponent component)
        {
            Action actions = Move;
            actions += Aim;
            actions += TryJump;
            component.AddBehaviour(typeof(PlayerBehaviourIdle), new PlayerBehaviourIdle(this, actions, logger));

            actions = Aim;
            component.AddBehaviour(typeof(PlayerBehaviourSit), new PlayerBehaviourSit(this, actions));
        }

        private void CameraPlaceHandleEvent(AEntity entity, CameraPlace e)
        {
            isFlip = e.flip;
        }

        public void SetZeroValues()
        {
            playerAnimator.Animate(movementDirection, 0);
        }

        public void EnableComponents() => currentController.EnableView();
        public void DisableComponents() => currentController.DisableView();

        public void Move()
        {
            if (isAimMode || !IsOwner) return;

            movementDirection = currentController.GetMove() * (isFlip ? -1 : 1);
            float movementMagnitude = movementDirection.magnitude;

            CheckBust(movementMagnitude);

            float movementSpeed = Mathf.Clamp(movementMagnitude, 0f, 1f);

            if (movementSpeed < movementMinimum) movementDirection = Vector2.zero;
            else MoveMark();

            timeSinceLastSend += Time.deltaTime;
            if (timeSinceLastSend >= sendInterval)
            {
                MoveServerRpc(movementDirection.normalized, movementSpeed);
                timeSinceLastSend = 0f;
            }

            if (isFlip) movementDirection.y *= -1;

            playerAnimator.Animate(movementDirection, movementSpeed);
        }

        private void CheckBust(float movementMagnitude)
        {
            forwardTap.Update(movementMagnitude > 0.9f);
            if (forwardTap.IsBoostActive && currentSpeed < 1f)
                currentSpeed = movementComponent.BaseSpeed / 2;
        }

        [ServerRpc]
        private void MoveServerRpc(Vector2 direction, float inputSpeed)
        {
            if (inputSpeed <= 0.01f)
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, movementComponent.Deceleration * Time.deltaTime);

            float targetSpeed = inputSpeed * movementComponent.BaseSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, movementComponent.Acceleration * Time.deltaTime);

            playerRigidbody.linearVelocity = new Vector3(
                direction.x * currentSpeed,
                playerRigidbody.linearVelocity.y,
                direction.y * currentSpeed
            );

            UpdateClientPositionClientRpc(playerRigidbody.position, playerRigidbody.linearVelocity);
        }

        [ClientRpc]
        private void UpdateClientPositionClientRpc(Vector3 position, Vector3 velocity)
        {
            if (IsOwner) return;

            playerRigidbody.position = position;
            playerRigidbody.linearVelocity = velocity;
        }

        private readonly Vector3 MarkOffset = new(0, 0, 100);
        private void MoveMark()
        {
            playerMark.position = transform.position + MarkOffset;
            float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90;
            playerMark.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void OnJumpStarted(InputAction.CallbackContext ctx)
        {
            if (!IsOwner || !groundChecker.IsGrounded) return;
            jumpCharge.StartCharge();
        }

        private void OnJumpCanceled(InputAction.CallbackContext ctx)
        {   
            if (!IsOwner) return;
            jumpCharge.Release();
        }
        
        public void TryJump()
        {
            if (!IsOwner || !jumpCharge.JumpRequested) return;
            jumpCharge.ConsumeJump();
            if (!groundChecker.IsGrounded) return;
            float forceMult = jumpForceCurve.Evaluate(jumpCharge.ChargeNormalized);
            JumpServerRpc(baseJumpForce * forceMult);
        }


        [ServerRpc]
        private void JumpServerRpc(float force)
        {
            Vector3 vel = playerRigidbody.linearVelocity;
            vel.y = force;
            playerRigidbody.linearVelocity = vel;

            UpdateClientPositionClientRpc(playerRigidbody.position, playerRigidbody.linearVelocity);
        }

        private void OnActionStarted(InputAction.CallbackContext ctx) => isHolding = true;
        private void OnActionCanceled(InputAction.CallbackContext ctx) => isHolding = false;

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

            crosshair.localPosition = magnitude < aimComponent.CrosshairDistance
                ? new Vector3(aim.x, 0, aim.y)
                : new Vector3(aimNorm.x * aimComponent.CrosshairDistance, 0, aimNorm.y * aimComponent.CrosshairDistance);

            crosshair.gameObject.SetActive(true);
            isAccurance = true;
        }

        private void SetAimAddition(float magnitude, Vector2 aimNorm)
        {
            float factMouseFactor = globalSettings.controlType == ControlType.Mobile && isAimMode ? 0.2f : 1f;

            if (magnitude > aimComponent.MaxCrosshairDistance * factMouseFactor)
                aimNorm *= aimComponent.MaxCrosshairDistance;
            else if (magnitude < aimComponent.CrosshairDistance * factMouseFactor + 1)
                aimNorm = Vector2.zero;

            entityEventBus.Invoke(playerEntity, new AimAddition { Position = aimNorm });
        }

        public void ChangeAimMode() => isAimMode = !isAimMode;

        public void AimRaiseMobile() => AimRaise(new InputAction.CallbackContext());
        public void AimPlaceMobile() => AimPlace(new InputAction.CallbackContext());

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
            else
            {
                entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = Extension.RoundVector2D(crosshair.position) });
            }
        }

        private void AimPlace(InputAction.CallbackContext obj)
        {
            if (crosshair.gameObject.activeSelf && canPlace)
                Placing().Forget();
        }

        private async UniTaskVoid Placing()
        {
            canPlace = false;
            entityEventBus.Invoke(playerEntity, new PlaceEvent { Position = Extension.RoundVector2D(crosshair.position) });
            await UniTask.Delay(placeObjectDelay);
            canPlace = true;
        }

        public void OnDisable()
        {
            unsubscribe.Dispose();
            currentController.MeetEnds();

            Raise.action.performed -= AimRaise;
            LeftClick.action.performed -= AimPlace;

            RightClick.action.started -= OnActionStarted;
            RightClick.action.canceled -= OnActionCanceled;

            Jump.action.started -= OnJumpStarted;
            Jump.action.canceled -= OnJumpCanceled;
        }
    }
}