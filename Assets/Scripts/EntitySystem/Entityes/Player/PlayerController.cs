using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Mathematics;
using Unity.Netcode;
using Cysharp.Threading.Tasks;
using R3;

using System;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.Events;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.ViewControl;
using TMPro;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetworkBehaviour
    {
        private const float SpawnHeightY = 200f;
        private const float JumpMaxChargeTime = 1f;

        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float reachTolerance = 2f;
        [SerializeField] private float maxLatencyCompensation = 0.5f;
        [SerializeField] private float aimHysteresis = 0.15f;
        [SerializeField] private float movementMinimum, sendInterval = 0.1f, maxStepHeight = 0.6f;
        [SerializeField] private float intentTimeout = 0.6f;
        [SerializeField] private float editRate = 20f, editBurst = 16f, refuseLogInterval = 5f;
        [SerializeField] private float moveRateMargin = 2f, moveBurst = 8f;
        [SerializeField] private float craftRate = 5f, craftBurst = 5f;
        [SerializeField] private float craftTimeTolerance = 0.9f;
        [SerializeField] private float jumpChargeSlack = 0.2f;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, Jump;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Transform crosshair;
        [SerializeField] private PlayerAnimator playerAnimator;

        [Header("Jump")]
        [SerializeField] private float baseJumpForce = 7f;
        [SerializeField] private AnimationCurve jumpForceCurve;
        [SerializeField] private Transform jumpIndicator;
        [SerializeField] private float jumpIndicatorHeight = 1.5f, jumpIndicatorWidth = 0.15f;
        [SerializeField] private float jumpIndicatorXOffset = 2f, jumpIndicatorYOffset = 2f;

        [Header("Sprint")]
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float staminaPerMaxEnergy = 0.5f, capAtZeroEnergy = 0.3f;
        [SerializeField] private float staminaDrainFraction = 0.2f, staminaRegenFraction = 0.15f;
        [SerializeField] private float staminaRecoveryDelay = 1f, energyPerStamina = 0.2f;
        [SerializeField] private Transform staminaFill;
        [SerializeField] private float staminaIndicatorXOffset = 2f, staminaIndicatorYOffset = 2f;

        [SerializeField] private float characterHalfHeight = 1f;
        [SerializeField] private float bodyRadius = 0.4f;
        private TerrainPhysicsResolver terrainResolver;

        private bool IsOnTerrain => terrainResolver.IsGrounded;
        private Rigidbody playerRigidbody;
        private Vector2 movementDirection;
        private float timeSinceLastSend;
        public float CurrentSpeed { get; private set; }
        private bool isAimMode;
        private bool canPlace = true;
        private double speechReadyTime;
        private bool isAccurance;
        private bool isHolding;
        private bool inputSuspended;
        private long aimCell = AimSnap.NoCell;
        private int aimLevel;
        private object aimPlaceable;
        private ObjectInfo aimInfo;
        private CameraInputController cameraInput;

        private IController currentController;
        private MapGenerator map;
        private RavineLogger logger;
        private MovementComponent movementComponent;
        private EventBus entityEventBus;
        private AimComponent aimComponent;
        private ControlType controlType;
        private bool controllerViewEnabled;
        private Camera playerCamera;
        private AEntity playerEntity;

        private readonly DoubleTapDetector forwardTap = new();
        private readonly JumpChargeDetector jumpCharge = new(maxChargeTime: JumpMaxChargeTime);
        private PlayerStamina stamina;
        private NetworkVariable<float> staminaRatio = new(1f);
        private IDisposable unsubscribe;
        private readonly ulong[] ownerTarget = new ulong[1];
        private readonly CompositeDisposable settingsSubscriptions = new();

        private Vector2 intentDirection;
        private Vector2 brakeDirection;
        private float intentSpeed;
        private bool intentSprint;
        private double intentTime;
        private float broadcastTimer;
        private Vector3 lastBroadcastPosition;
        private Vector3 lastBroadcastVelocity;
        private RateBudget editBudget;
        private RateBudget moveBudget;
        private RateBudget craftBudget;
        private double lastJumpTime;
        private double refuseLogTime;
        private int refusedEdits;
        private float vitalsTimer;
        private double craftReadyTime;
        private MortalityComponent mortalityComponent;
        private EnergyComponent energyComponent;
        private float mirroredHealth = float.NaN, mirroredEnergy = float.NaN;
        private StatsComponent stats;
        private readonly PlayerItemLedger ledger = new();
        private readonly NetworkVariable<float> netHealth = new(float.NaN);
        private readonly NetworkVariable<float> netEnergy = new(float.NaN);

        public void SetInitialValues(AEntity entity, RavineLogger logger)
        {
            playerEntity = entity;
            this.logger = logger;
            lastJumpTime = Time.timeAsDouble;

            playerAnimator.SetUpAsync().Forget();
            GetPlayerComponents();
            AttachViewOrientation();
            stamina = new PlayerStamina(
                playerEntity.GetEntityComponent<EnergyComponent>(),
                staminaPerMaxEnergy, capAtZeroEnergy, staminaDrainFraction,
                staminaRegenFraction, staminaRecoveryDelay, energyPerStamina);

            Raise.action.performed += AimRaise;

            RightClick.action.started += OnActionStarted;
            RightClick.action.canceled += OnActionCanceled;

            Jump.action.started += OnJumpStarted;
            Jump.action.canceled += OnJumpCanceled;
            
        }

        public void SetUp()
        {
            map = ServiceLocator.GetService<MapGenerator>();
            terrainResolver = new TerrainPhysicsResolver(map, playerRigidbody, characterHalfHeight, maxStepHeight, bodyRadius);
            WorldRegistry worldRegistry = ServiceLocator.GetService<WorldRegistry>();

            if (IsOwner)
            {
                playerCamera = playerEntity.GetEntityComponent<CameraComponent>()?.GetCamera();

                ServiceLocator.GetService<GlobalSettingsController>().Settings
                    .Select(s => s.controlType)
                    .DistinctUntilChanged()
                    .Subscribe(ApplyControlType)
                    .AddTo(settingsSubscriptions);

                unsubscribe = ServiceLocator.GetService<AutosaveCoordinator>().SubscribeBeforeSave(() =>
                {
                    SavePlayerPosition(worldRegistry);
                });
            }
            DelayedInit(worldRegistry).Forget();
        }

        private void ApplyControlType(ControlType type)
        {
            controlType = type;

            if (currentController != null)
            {
                currentController.DisableView();
                currentController.MeetEnds();
                (currentController as IDisposable)?.Dispose();
            }

            currentController = type switch
            {
                ControlType.Personal => new PCController(Movement, playerCamera, transform, aimComponent.CrosshairDistance),
                ControlType.Mobile => new JoistickController(joystick),
                _ => null
            };

            if (controllerViewEnabled) currentController?.EnableView();
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

            if (IsServer)
            {
                WorldState worldState = IsOwner ? worldRegistry.GetCurrentState() : null;

                if (worldState != null && !worldState.playerPosition.IsNull())
                {
                    transform.position = worldState.playerPosition.ToSpawnPoint();
                }
                else
                {
                    Vector2 spawnPoint = Extension.GetRandomPointAround(transform.position, 10);
                    transform.position = new Vector3(spawnPoint.x, SpawnHeightY, spawnPoint.y);
                }
            }

            await UniTask.Delay(5000);
            playerRigidbody.useGravity = true;
            terrainResolver.SetActive(true);  
        }

        private void GetPlayerComponents()
        {
            playerRigidbody = GetComponent<Rigidbody>();

            entityEventBus = playerEntity.GetEntityComponent<EventBusComponent>().EventBus;
            entityEventBus.Subscribe<ViewClick>(OnViewClick);
            movementComponent = playerEntity.GetEntityComponent<MovementComponent>();
            aimComponent = playerEntity.GetEntityComponent<AimComponent>();
            stats = playerEntity.GetEntityComponent<StatsComponent>();
            mortalityComponent = playerEntity.GetEntityComponent<MortalityComponent>();
            energyComponent = playerEntity.GetEntityComponent<EnergyComponent>();
            InitStatePattern(playerEntity.GetEntityComponent<StatePatternComponent>());
        }

        private void AttachViewOrientation()
        {
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
                ViewOrientedTransform.Attach(sprites[i].gameObject, ViewOrientMode.Yaw);

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                ViewOrientedTransform.Attach(texts[i].gameObject, ViewOrientMode.Yaw);

            if (jumpIndicator != null) ViewOrientedTransform.Attach(jumpIndicator.gameObject, ViewOrientMode.Yaw);
            if (staminaFill != null) ViewOrientedTransform.Attach(staminaFill.gameObject, ViewOrientMode.Yaw);
        }

        private void InitStatePattern(StatePatternComponent component)
        {
            Action actions = Move;
            actions += Aim;
            actions += TryJump;
            component.AddBehaviour(typeof(PlayerBehaviourIdle), new PlayerBehaviourIdle(this, actions, logger));

            component.AddBehaviour(typeof(PlayerBehaviourSit), new PlayerBehaviourSit(this, null));
        }

        public void SetZeroValues()
        {
            playerAnimator.Animate(movementDirection, 0);
            movementDirection = Vector2.zero;
        }

        public void Suspend()
        {
            inputSuspended = true;
            isHolding = false;
            jumpCharge.Cancel();
            ResetAim();
            SetZeroValues();
            timeSinceLastSend = 0f;
            if (IsSpawned && IsOwner) StopMoveServerRpc();
        }

        public void Resume()
        {
            inputSuspended = false;
        }

        private void ResetAim()
        {
            if (crosshair != null) crosshair.gameObject.SetActive(false);
            isAccurance = false;
            aimCell = AimSnap.NoCell;
            aimLevel = 0;
            aimPlaceable = null;
            if (cameraInput != null) cameraInput.WheelCaptured = false;
        }

        public void EnableComponents()
        {
            controllerViewEnabled = true;
            currentController?.EnableView();
        }

        public void DisableComponents()
        {
            controllerViewEnabled = false;
            currentController?.DisableView();
        }


        private void FixedUpdate()
        {
            if (!IsServer) return;
            terrainResolver?.Resolve();
            StepServerMovement(Time.fixedDeltaTime);
            PublishVitals(Time.fixedDeltaTime);
        }

        private void PublishVitals(float dt)
        {
            vitalsTimer += dt;
            if (vitalsTimer < sendInterval || stats == null || stats.IsDisposed) return;
            vitalsTimer = 0f;

            netHealth.Value = stats.Hp;
            netEnergy.Value = stats.En;
        }

        private void MirrorVitals()
        {
            float health = netHealth.Value, energyValue = netEnergy.Value;
            if (!math.isfinite(health) || !math.isfinite(energyValue)) return;
            if (health == mirroredHealth && energyValue == mirroredEnergy) return;
            if (playerEntity is not PlayerEntity player) return;

            mirroredHealth = health;
            mirroredEnergy = energyValue;
            player.ApplyServerVitals(health, energyValue);
        }

        private void Update()
        {
            if (IsSpawned && !IsServer) MirrorVitals();
            if (!IsOwner) return;

            UpdateJumpIndicator();
            if (staminaFill != null) 
            {
                staminaFill.localScale = new Vector3(staminaRatio.Value, jumpIndicatorWidth, jumpIndicatorWidth);
                staminaFill.localPosition = ViewOrientation.YawRotation * new Vector3(staminaIndicatorXOffset - staminaRatio.Value * 0.5f, staminaIndicatorYOffset, 0f);
            }
        }

        private void UpdateJumpIndicator()
        {
            if (!jumpCharge.IsCharging)
            {
                if (jumpIndicator != null) jumpIndicator.gameObject.SetActive(false);
                return;
            }

            float height = jumpForceCurve.Evaluate(jumpCharge.LiveChargeNormalized) * jumpIndicatorHeight;
            jumpIndicator.gameObject.SetActive(true);
            jumpIndicator.localScale = new Vector3(jumpIndicatorWidth, height, jumpIndicatorWidth);
            jumpIndicator.localPosition = ViewOrientation.YawRotation * new Vector3(jumpIndicatorXOffset, jumpIndicatorYOffset + height * 0.5f, 0f);
        }

        private static bool IsSprintHeld() => Keyboard.current != null && Keyboard.current.rightCtrlKey.isPressed;

        public void Move()
        {
            if (isAimMode || !IsOwner || inputSuspended) return;

            Vector2 screenInput = currentController.GetMove();
            float2 world = ViewOrientation.ToWorld(screenInput);
            movementDirection = new Vector2(world.x, world.y);
            float movementMagnitude = movementDirection.magnitude;

            CheckBust(movementMagnitude);

            float movementSpeed = Mathf.Clamp(movementMagnitude, 0f, 1f);

            if (movementSpeed < movementMinimum)
            {
                movementDirection = Vector2.zero;
                screenInput = Vector2.zero;
            }

            timeSinceLastSend += Time.deltaTime;
            if (timeSinceLastSend >= sendInterval)
            {
                MoveServerRpc(movementDirection.normalized, movementSpeed, IsSprintHeld());
                timeSinceLastSend = 0f;
            }

            playerAnimator.Animate(screenInput, movementSpeed);
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
            if (!TryConsumeMove()) return;
            if (!math.isfinite(direction.x) || !math.isfinite(direction.y) || !math.isfinite(inputSpeed)) return;

            float directionSq = direction.sqrMagnitude;
            if (directionSq > 1f) direction *= math.rsqrt(directionSq);

            intentDirection = direction;
            intentSpeed = math.saturate(inputSpeed);
            intentSprint = sprint;
            intentTime = Time.timeAsDouble;
        }

        private void StepServerMovement(float dt)
        {
            if (stamina == null || map == null || terrainResolver == null || movementComponent == null || playerRigidbody == null) return;

            if (Time.timeAsDouble - intentTime > intentTimeout)
            {
                intentDirection = Vector2.zero;
                intentSpeed = 0f;
                intentSprint = false;
            }

            Vector2 direction = intentDirection;
            float inputSpeed = intentSpeed;
            if (direction.sqrMagnitude > 0f) brakeDirection = Vector2.zero;

            ref readonly var rules = ref SimulationRules.Frame;
            float ground = map.SampleHeightBilinear(playerRigidbody.position.x, playerRigidbody.position.z);
            bool sprinting = stamina.Step(dt, intentSprint, direction.sqrMagnitude > 0f,
                MapGenerator.WaterModifier(ground, rules.WaterLevel, rules.WaterDrainMul));

            float targetSpeed = inputSpeed * movementComponent.BaseSpeed * (sprinting ? sprintMultiplier : 1f)
                * MapGenerator.WaterModifier(ground, rules.WaterLevel, rules.WaterSpeedMul);
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
                float step = rate * dt;
                float delta = targetSpeed - CurrentSpeed;
                CurrentSpeed = math.abs(delta) <= step ? targetSpeed : CurrentSpeed + math.sign(delta) * step;
            }

            Vector2 travel = direction.sqrMagnitude > 0f ? direction : brakeDirection;
            float velocityX = travel.x * CurrentSpeed;
            float velocityZ = travel.y * CurrentSpeed;
            terrainResolver.ClipVelocity(ref velocityX, ref velocityZ);
            playerRigidbody.linearVelocity = new Vector3(
                velocityX,
                playerRigidbody.linearVelocity.y,
                velocityZ
            );

            broadcastTimer += dt;
            if (broadcastTimer < sendInterval) return;
            broadcastTimer = 0f;

            staminaRatio.Value = stamina.Ratio;
            BroadcastState();
        }

        private void BroadcastState()
        {
            Vector3 position = playerRigidbody.position;
            Vector3 velocity = playerRigidbody.linearVelocity;
            if ((position - lastBroadcastPosition).sqrMagnitude < 1e-6f && (velocity - lastBroadcastVelocity).sqrMagnitude < 1e-6f) return;

            lastBroadcastPosition = position;
            lastBroadcastVelocity = velocity;
            UpdateClientPositionClientRpc(position, velocity);
        }

        [ServerRpc]
        private void StopMoveServerRpc()
        {
            if (!TryConsumeMove()) return;

            if (intentDirection.sqrMagnitude > 0f) brakeDirection = intentDirection;
            intentDirection = Vector2.zero;
            intentSpeed = 0f;
            intentSprint = false;
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
            if (!IsOwner || inputSuspended) return;
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
            JumpServerRpc(jumpCharge.ChargeNormalized);
        }


        [ServerRpc]
        private void JumpServerRpc(float charge)
        {
            if (!TryConsumeMove()) return;
            if (!math.isfinite(charge)) return;
            if (terrainResolver == null || !terrainResolver.IsGrounded) return;
            terrainResolver.ConsumeGround();

            double now = Time.timeAsDouble;
            charge = JumpChargeLimit.Clamp(charge, now - lastJumpTime, JumpMaxChargeTime, jumpChargeSlack);
            lastJumpTime = now;

            Vector3 vel = playerRigidbody.linearVelocity;
            vel.y = baseJumpForce * jumpForceCurve.Evaluate(charge);
            playerRigidbody.linearVelocity = vel;

            BroadcastState();
        }

        private void OnActionStarted(InputAction.CallbackContext ctx) => isHolding = !inputSuspended;
        private void OnActionCanceled(InputAction.CallbackContext ctx) => isHolding = false;

        public void Aim()
        {
            if (!isHolding || inputSuspended)
            {
                ResetAim();
                return;
            }

            if (map == null) return;
            UpdateAimLevel();

            Vector2 aim = currentController.GetAim();
            Vector2 aimNorm = aim.normalized;
            float magnitude = aim.magnitude;

            SetAimAddition(magnitude, aimNorm);

            Vector3 self = transform.position;
            float2 target = magnitude < aimComponent.CrosshairDistance
                ? new float2(aim.x + self.x, aim.y + self.z)
                : new float2(aimNorm.x * aimComponent.CrosshairDistance + self.x, aimNorm.y * aimComponent.CrosshairDistance + self.z);

            aimCell = AimSnap.Resolve(target, aimCell, aimHysteresis);
            Vector3 aimPosition = map.GetRealPosition(aimCell);
            if (aimPlaceable == null && map.TryGetObject(aimCell, aimLevel, out ObjectInstInfo aimObject))
                aimPosition = aimObject.Position;
            else
                aimPosition.y = map.PlacementY(aimCell, aimInfo, aimLevel);
            crosshair.position = aimPosition;

            crosshair.gameObject.SetActive(true);
            isAccurance = true;
        }

        private void UpdateAimLevel()
        {
            if (cameraInput == null && !ServiceLocator.Services.TryGet(out cameraInput)) return;
            cameraInput.WheelCaptured = true;

            int placeableLevels = 1;
            object placeable = null;
            IPlacementSource placement = null;
            if (ServiceLocator.Services.TryGet(out placement))
            {
                placeable = placement.ActivePlaceable;
                if (placeable != null) placeableLevels = placement.ActivePlaceableLevels;
            }

            if (!ReferenceEquals(placeable, aimPlaceable))
            {
                aimPlaceable = placeable;
                aimInfo = null;
                int aimObjectId = placeable != null ? placement.ActivePlaceableObjectId : -1;
                if (aimObjectId >= 0 && ServiceLocator.Services.TryGet(out ObjectSystem aimObjects))
                    aimInfo = aimObjects.GetInfo(aimObjectId);
                aimLevel = 0;
                cameraInput.ConsumeWheelStep();
            }

            int maxAimLevel = math.max(0, ObjectInfo.MaxLevels - placeableLevels);
            aimLevel = math.clamp(aimLevel + cameraInput.ConsumeWheelStep(), 0, maxAimLevel);
        }

        private void SetAimAddition(float magnitude, Vector2 aimNorm)
        {
            float factMouseFactor = controlType == ControlType.Mobile && isAimMode ? 0.2f : 1f;

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
            if (inputSuspended) return;
            if (!isAccurance)
            {
                int currentX = Mathf.RoundToInt(transform.position.x);
                int currentZ = Mathf.RoundToInt(transform.position.z);
                for (int xOffset = -aimComponent.PickDistance; xOffset <= aimComponent.PickDistance; xOffset++)
                    for (int zOffset = -aimComponent.PickDistance; zOffset <= aimComponent.PickDistance; zOffset++)
                        entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = Position2Int.Pack(currentX + xOffset, currentZ + zOffset) });
            }
            else if (aimCell != AimSnap.NoCell)
            {
                entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = aimCell, Level = (byte)aimLevel });
            }
        }

        private void OnViewClick(AEntity sender, ViewClick e) => AimPlace(new InputAction.CallbackContext());

        private void AimPlace(InputAction.CallbackContext obj)
        {
            if (inputSuspended) return;
            if (crosshair.gameObject.activeSelf && canPlace && aimCell != AimSnap.NoCell)
                Placing().Forget();
        }

        private async UniTaskVoid Placing()
        {
            canPlace = false;
            
            long position = aimCell;
            byte level = (byte)aimLevel;
            if (ServiceLocator.Services.TryGet(out IPlacementSource placement) && placement.HasPlaceableInActiveCell())
                entityEventBus.Invoke(playerEntity, new PlaceEvent { Position = position, Level = level });
            else
                entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = position, Level = level });

            await UniTask.Delay(placeObjectDelay);
            canPlace = true;
        }

        public bool RequestPlace(long position, int objectId, byte level = 0)
        {
            if (!IsSpawned || !IsOwner) return false;
            PlaceRequestServerRpc(position, objectId, level);
            return true;
        }

        public bool RequestSpeak(ushort tokens, byte len)
        {
            if (!IsSpawned || !IsOwner) return false;
            SpeakServerRpc(tokens, len);
            return true;
        }

        [ServerRpc]
        private void SpeakServerRpc(ushort tokens, byte len)
        {
            if (len == 0 || len > SimulationRules.SpeechLen) return;
            if ((tokens >> (len * SimulationRules.SpeechTokenBits)) != 0) return;
            if (!SpeechBus.TryStartCooldown(ref speechReadyTime)) return;

            if (playerEntity == null || playerEntity.IsDisposed || !playerEntity.IsActive.Value) return;

            MortalityComponent mortality = mortalityComponent ??= playerEntity.GetEntityComponent<MortalityComponent>();
            if (mortality != null && mortality.IsDead) return;

            EnergyComponent energy = energyComponent ??= playerEntity.GetEntityComponent<EnergyComponent>();
            if (energy == null || !ServiceLocator.Services.TryGet(out SpeechBus bus)) return;
            if (!energy.TryConsume(SimulationRules.Active.SpeechEnergyCost)) return;

            Vector3 p = transform.position;
            var utterance = new Utterance
            {
                Tokens   = tokens,
                Len      = len,
                Loudness = (byte)SimulationRules.Frame.SpeechLoudness,
                Speaker  = Utterance.External,
                Pos      = new float2(p.x, p.z),
                Time     = SimulationClock.Time,
            };
            bus.Emit(in utterance);
        }

        public bool RequestPickUp(long position, byte level = 0)
        {
            if (!IsSpawned || !IsOwner) return false;
            PickUpRequestServerRpc(position, level);
            return true;
        }

        public bool RequestCraft(int recipeIndex)
        {
            if (!IsSpawned || !IsOwner || IsServer || recipeIndex < 0) return false;
            CraftRequestServerRpc(recipeIndex);
            return true;
        }

        private bool UsesLedger => IsServer && !IsOwner;

        [ServerRpc]
        private void CraftRequestServerRpc(int recipeIndex)
        {
            if (!UsesLedger || !TryConsumeCraft()) return;
            if (!CraftModel.Shared.TryGet(recipeIndex, out CraftRecipe recipe)) return;

            double now = Time.unscaledTimeAsDouble;
            if (now < craftReadyTime) return;
            if (!ledger.TryCraft(recipe)) return;

            craftReadyTime = now + math.max(0f, recipe.TimeToComplete) * craftTimeTolerance;
        }

        [ServerRpc]
        private void PlaceRequestServerRpc(long position, int objectId, byte level)
        {
            WorldEditResult result = WorldEditResult.Refused;
            PlacementResult placement = PlacementResult.Ok;
            if (aimComponent != null && TryResolveEditor(position, aimComponent.CrosshairDistance, out MapGenerator generator, out ObjectSystem objects))
            {
                IInventoryItemInfo item = UsesLedger ? objects.GetInfo(objectId)?.InventoryItemInfo : null;
                if (UsesLedger && ledger.Count(item) < 1)
                    result = WorldEditResult.NotOwned;
                else
                    result = WorldEdits.TryPlace(generator, objects, position, level, objectId, out placement);

                if (result == WorldEditResult.Ok && UsesLedger) ledger.TryDebit(item, 1);
                if (result != WorldEditResult.Ok) Refuse(position, EditRefusal.Place, result, placement);
            }
            PlaceResultClientRpc(position, level, objectId, (byte)result, (byte)placement, OwnerTarget());
        }

        [ServerRpc]
        private void PickUpRequestServerRpc(long position, byte level)
        {
            bool ok = false;
            int prefabId = ObjectInstInfo.PlaceholderId, amount = 0;
            if (aimComponent != null && TryResolveEditor(position,
                    math.max(aimComponent.PickDistance * math.SQRT2, aimComponent.CrosshairDistance),
                    out MapGenerator generator, out ObjectSystem objects))
            {
                WorldEditResult result = WorldEdits.TryPickUp(generator, objects, position, level, out prefabId, out amount);
                ok = result == WorldEditResult.Ok;
                if (ok && UsesLedger) ledger.Credit(objects.GetInfo(prefabId)?.InventoryItemInfo, amount);
                if (!ok) Refuse(position, EditRefusal.PickUp, result);
            }
            PickUpResultClientRpc(position, level, prefabId, amount, ok, OwnerTarget());
        }

        [ClientRpc]
        private void PlaceResultClientRpc(long position, byte level, int objectId, byte result, byte placement, ClientRpcParams rpcParams = default)
        {
            bool ok = result == (byte)WorldEditResult.Ok;
            if (ok && !IsServer && ServiceLocator.Services.TryGet(out MapGenerator generator)
                && ServiceLocator.Services.TryGet(out ObjectSystem objects))
                WorldEdits.MirrorPlace(generator, objects, position, level, objectId);

            entityEventBus?.Invoke(playerEntity, new PlaceResultEvent { Position = position, Level = level, Ok = ok, Edit = result, Placement = placement });
        }

        [ClientRpc]
        private void PickUpResultClientRpc(long position, byte level, int prefabId, int amount, bool ok, ClientRpcParams rpcParams = default)
        {
            if (ok && !IsServer && ServiceLocator.Services.TryGet(out MapGenerator generator))
                WorldEdits.MirrorPickUp(generator, position, level, prefabId);

            entityEventBus?.Invoke(playerEntity, new PickUpResultEvent { Position = position, Level = level, PrefabId = prefabId, Amount = amount, Ok = ok });
        }

        private bool TryResolveEditor(long position, float reach, out MapGenerator generator, out ObjectSystem objects)
        {
            generator = null;
            objects = null;

            if (!TryConsumeEdit())
                return Refuse(position, EditRefusal.Rate);

            if (playerEntity == null || playerEntity.IsDisposed || !playerEntity.IsActive.Value)
                return Refuse(position, EditRefusal.Inactive);

            MortalityComponent mortality = mortalityComponent ??= playerEntity.GetEntityComponent<MortalityComponent>();
            if (mortality != null && mortality.IsDead)
                return Refuse(position, EditRefusal.Dead);

            Vector3 self = transform.position;
            float2 delta = new(Position2Int.GetX(position) - self.x, Position2Int.GetY(position) - self.z);
            float limit = reach + reachTolerance + LatencyCompensation();
            if (math.lengthsq(delta) > limit * limit)
                return Refuse(position, EditRefusal.OutOfReach);

            if (!ServiceLocator.Services.TryGet(out generator) || !ServiceLocator.Services.TryGet(out objects))
                return Refuse(position, EditRefusal.NotReady);

            return true;
        }

        private bool TryConsumeEdit() => editBudget.TryConsume(Time.unscaledTimeAsDouble, editRate, editBurst);

        private bool TryConsumeMove()
        {
            if (moveBudget.TryConsume(Time.unscaledTimeAsDouble, moveRateMargin / math.max(sendInterval, 0.001f), moveBurst)) return true;

            refusedEdits++;
            return false;
        }

        private bool TryConsumeCraft()
        {
            if (craftBudget.TryConsume(Time.unscaledTimeAsDouble, craftRate, craftBurst)) return true;

            refusedEdits++;
            return false;
        }

        private float LatencyCompensation()
        {
            if (movementComponent == null || playerRigidbody == null) return 0f;

            Vector3 velocity = playerRigidbody.linearVelocity;
            float maxSpeed = math.max(0f, movementComponent.BaseSpeed * sprintMultiplier);
            float speedXZ = math.clamp(math.length(new float2(velocity.x, velocity.z)), 0f, maxSpeed);
            if (speedXZ <= 0f) return 0f;

            NetworkManager network = NetworkManager;
            NetworkTransport transport = network != null ? network.NetworkConfig?.NetworkTransport : null;
            float rtt = transport != null ? transport.GetCurrentRtt(OwnerClientId) * 0.001f : 0f;
            return speedXZ * (math.min(rtt, math.max(0f, maxLatencyCompensation)) * 0.5f + sendInterval);
        }

        private enum EditRefusal : byte
        {
            Rate,
            Inactive,
            Dead,
            OutOfReach,
            NotReady,
            Place,
            PickUp
        }

        private bool Refuse(long position, EditRefusal reason, WorldEditResult edit = WorldEditResult.Refused, PlacementResult placement = PlacementResult.Ok)
        {
            refusedEdits++;
            double now = Time.unscaledTimeAsDouble;
            if (now < refuseLogTime) return false;

            refuseLogTime = now + refuseLogInterval;
            RavineLog.Warning($"[PlayerController] Client {OwnerClientId}: {refusedEdits} edits refused, last at {Position2Int.UnpackToVector(position)}: {reason} {edit}/{placement}");
            refusedEdits = 0;
            return false;
        }

        private ClientRpcParams OwnerTarget()
        {
            ownerTarget[0] = OwnerClientId;
            return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = ownerTarget } };
        }

        public void OnDisable()
        {
            Raise.action.performed -= AimRaise;
            entityEventBus?.Unsubscribe<ViewClick>(OnViewClick);

            RightClick.action.started -= OnActionStarted;
            RightClick.action.canceled -= OnActionCanceled;

            Jump.action.started -= OnJumpStarted;
            Jump.action.canceled -= OnJumpCanceled;

            if (cameraInput != null) cameraInput.WheelCaptured = false;
            settingsSubscriptions.Dispose();
            currentController?.MeetEnds();
            unsubscribe?.Dispose();
        }
    }
}