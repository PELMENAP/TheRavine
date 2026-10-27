using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Mathematics;
using Unity.Netcode;
using Cysharp.Threading.Tasks;

using System;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.Events;
using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetworkBehaviour
    {
        private const float SpawnHeightY = 200f;

        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float movementMinimum, sendInterval = 0.1f, maxStepHeight = 0.6f;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick, Jump;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Transform crosshair;
        [SerializeField] private PlayerAnimator playerAnimator;

        [Header("Jump")]
        [SerializeField] private float baseJumpForce = 7f;
        [SerializeField] private AnimationCurve jumpForceCurve;
        [SerializeField] private Transform jumpIndicator;
        [SerializeField] private float jumpIndicatorHeight = 1.5f, jumpIndicatorWidth = 0.15f, jumpIndicatorBase = 1f;

        [Header("Sprint")]
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float staminaPerMaxEnergy = 0.5f, capAtZeroEnergy = 0.3f;
        [SerializeField] private float staminaDrainFraction = 0.2f, staminaRegenFraction = 0.15f;
        [SerializeField] private float staminaRecoveryDelay = 1f, energyPerStamina = 0.2f;
        [SerializeField] private Image staminaFill;

        [SerializeField] private float characterHalfHeight = 1f;
        private TerrainPhysicsResolver terrainResolver;

        private bool IsOnTerrain => terrainResolver.IsGrounded;
        private Rigidbody playerRigidbody;
        private Vector2 movementDirection;
        private float timeSinceLastSend;
        public float CurrentSpeed { get; private set; }
        private bool isAimMode;
        private bool canPlace = true;
        private bool isAccurance;
        private bool isHolding;
        private bool isFlip;

        private IController currentController;
        private MapGenerator map;
        private RavineLogger logger;
        private MovementComponent movementComponent;
        private EventBus entityEventBus;
        private AimComponent aimComponent;
        private GlobalSettings globalSettings;
        private AEntity playerEntity;

        private readonly DoubleTapDetector forwardTap = new();
        private readonly JumpChargeDetector jumpCharge = new(maxChargeTime: 1f);
        private PlayerStamina stamina;
        private NetworkVariable<float> staminaRatio = new(1f);
        private IDisposable unsubscribe;

        public void SetInitialValues(AEntity entity, RavineLogger logger)
        {
            playerEntity = entity;
            globalSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
            this.logger = logger;

            playerAnimator.SetUpAsync().Forget();
            GetPlayerComponents();
            stamina = new PlayerStamina(
                playerEntity.GetEntityComponent<EnergyComponent>(),
                staminaPerMaxEnergy, capAtZeroEnergy, staminaDrainFraction,
                staminaRegenFraction, staminaRecoveryDelay, energyPerStamina);

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

            map = ServiceLocator.GetService<MapGenerator>();
            terrainResolver = new TerrainPhysicsResolver(map, playerRigidbody, characterHalfHeight, maxStepHeight);

            WorldRegistry worldRegistry = ServiceLocator.GetService<WorldRegistry>();
            unsubscribe = ServiceLocator.GetService<AutosaveCoordinator>().SubscribeBeforeSave(() =>
            {
                SavePlayerPosition(worldRegistry);
            });
            DelayedInit(worldRegistry).Forget();
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
            terrainResolver.SetActive(false); 

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
            terrainResolver.SetActive(true);  
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


        private void FixedUpdate()
        {
            if (!IsServer) return;
            terrainResolver?.Resolve();
        }
        private void Update()
        {
            if (!IsOwner) return;

            UpdateJumpIndicator();
            if (staminaFill != null) staminaFill.fillAmount = staminaRatio.Value;
        }

        private void UpdateJumpIndicator()
        {
            if (!jumpCharge.IsCharging)
            {
                if (jumpIndicator != null) jumpIndicator.gameObject.SetActive(false);
                return;
            }

            if (jumpIndicator == null) jumpIndicator = CreateJumpIndicator();

            float height = jumpForceCurve.Evaluate(jumpCharge.LiveChargeNormalized) * jumpIndicatorHeight;
            jumpIndicator.gameObject.SetActive(true);
            jumpIndicator.localScale = new Vector3(jumpIndicatorWidth, height, jumpIndicatorWidth);
            jumpIndicator.localPosition = new Vector3(0f, jumpIndicatorBase + height * 0.5f, 0f);
        }

        private Transform CreateJumpIndicator()
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "JumpIndicator";
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(transform, false);
            return bar.transform;
        }

        private static bool IsSprintHeld() => Keyboard.current != null && Keyboard.current.rightCtrlKey.isPressed;

        public void Move()
        {
            if (isAimMode || !IsOwner) return;

            movementDirection = currentController.GetMove() * (isFlip ? -1 : 1);
            float movementMagnitude = movementDirection.magnitude;

            CheckBust(movementMagnitude);

            float movementSpeed = Mathf.Clamp(movementMagnitude, 0f, 1f);

            if (movementSpeed < movementMinimum) movementDirection = Vector2.zero;

            timeSinceLastSend += Time.deltaTime;
            if (timeSinceLastSend >= sendInterval)
            {
                MoveServerRpc(movementDirection.normalized, movementSpeed, IsSprintHeld());
                timeSinceLastSend = 0f;
            }

            if (isFlip) movementDirection.y *= -1;

            playerAnimator.Animate(movementDirection, movementSpeed);
        }

        private void CheckBust(float movementMagnitude)
        {
            forwardTap.Update(movementMagnitude > 0.9f);
            if (forwardTap.IsBoostActive && CurrentSpeed < 1f)
                CurrentSpeed = movementComponent.BaseSpeed / 2;
        }
        

        [ServerRpc]
        private void MoveServerRpc(Vector2 direction, float inputSpeed, bool sprint)
        {
            bool sprinting = stamina.Step(sendInterval, sprint, direction.sqrMagnitude > 0f);
            staminaRatio.Value = stamina.Ratio;

            float targetSpeed = inputSpeed * movementComponent.BaseSpeed * (sprinting ? sprintMultiplier : 1f);
            float terrainModifier = 1f;

            if (IsOnTerrain)
            {
                terrainModifier = map.GetSpeedModifier(
                    playerRigidbody.position.x,
                    playerRigidbody.position.z,
                    direction);

                targetSpeed *= terrainModifier;
            }

            if (terrainModifier <= 0f)
            {
                CurrentSpeed = 0f;
            }
            else
            {
                float rate = inputSpeed <= 0.01f ? movementComponent.Deceleration : movementComponent.Acceleration;
                float step = rate * sendInterval;
                float delta = targetSpeed - CurrentSpeed;
                CurrentSpeed = math.abs(delta) <= step ? targetSpeed : CurrentSpeed + math.sign(delta) * step;
            }

            playerRigidbody.linearVelocity = new Vector3(
                direction.x * CurrentSpeed,
                playerRigidbody.linearVelocity.y,
                direction.y * CurrentSpeed
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
        private void OnJumpStarted(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
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
            float forceMultiplier = jumpForceCurve.Evaluate(jumpCharge.ChargeNormalized);
            JumpServerRpc(baseJumpForce * forceMultiplier);
        }


        [ServerRpc]
        private void JumpServerRpc(float force)
        {
            if (terrainResolver == null || !terrainResolver.IsGrounded) return;
            terrainResolver.ConsumeGround();

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

            crosshair.position = magnitude < aimComponent.CrosshairDistance
                ? new Vector3(aim.x + transform.position.x, transform.position.y, aim.y + transform.position.z)
                : new Vector3(aimNorm.x * aimComponent.CrosshairDistance + transform.position.x, transform.position.y, aimNorm.y * aimComponent.CrosshairDistance + transform.position.z);

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
                        entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = Position2Int.Pack(currentX + xOffset, currentZ + zOffset) });
            }
            else
            {
                int currentX = Mathf.RoundToInt(crosshair.position.x);
                int currentZ = Mathf.RoundToInt(crosshair.position.z);
                entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = Position2Int.Pack(currentX, currentZ) });
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
            
            int currentX = Mathf.RoundToInt(crosshair.position.x);
            int currentZ = Mathf.RoundToInt(crosshair.position.z);
            entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = Position2Int.Pack(currentX, currentZ) });

            await UniTask.Delay(placeObjectDelay);
            canPlace = true;
        }

        public void OnDisable()
        {
            Raise.action.performed -= AimRaise;
            LeftClick.action.performed -= AimPlace;

            RightClick.action.started -= OnActionStarted;
            RightClick.action.canceled -= OnActionCanceled;

            Jump.action.started -= OnJumpStarted;
            Jump.action.canceled -= OnJumpCanceled;

            currentController?.MeetEnds();
            unsubscribe?.Dispose();
        }
    }
}