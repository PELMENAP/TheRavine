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
    public class PlayerController : NetworkBehaviour, IEntityController
    {
        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float movementMinimum, sendInterval = 0.1f;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Transform crosshair, playerMark;
        [SerializeField] private PlayerAnimator playerAnimator;
        private Rigidbody playerRigidbody;
        private Vector2 movementDirection;
        private Vector2 lastSentDirection;
        private float timeSinceLastSend;
        private bool isAimMode;
        private bool act = true;
        private bool isAccurance;

        private IController currentController;
        private IRavineLogger logger;
        private MovementComponent movementComponent;
        private EventBus entityEventBus;
        private AimComponent aimComponent;
        private GameSettings gameSettings;
        private AEntity playerEntity;
        public void SetInitialValues(AEntity entity, IRavineLogger logger)
        {
            playerEntity = entity;
            gameSettings = ServiceLocator.GetService<SettingsModel>().GameSettings.CurrentValue;
            this.logger = logger;
            Vector2 spawnSpread = Extension.GetRandomPointAround(this.transform.position, 10);
            this.transform.position = new Vector3(spawnSpread.x, 10, spawnSpread.y);
            

            playerAnimator.SetUpAsync().Forget();

            currentController = gameSettings.controlType switch
            {
                ControlType.Personal => new PCController(Movement, RightClick, transform),
                ControlType.Mobile => new JoistickController(joystick),
                _ => throw new NotImplementedException()
            };


            GetPlayerComponents();
            DelayedInit().Forget();

            Raise.action.performed += AimRaise;
            LeftClick.action.performed += AimPlace;
        }

        private async UniTaskVoid DelayedInit()
        {
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
        }

        private void InitStatePattern(StatePatternComponent component)
        {
            Action actions = Move;
            actions += Aim;
            component.AddBehaviour(typeof(PlayerBehaviourIdle), new PlayerBehaviourIdle (this, actions, logger));
            
            actions += Aim;
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
            float movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0f, 1f);

            if (movementSpeed < movementMinimum) movementDirection = Vector2.zero;
            else MoveMark();

            timeSinceLastSend += Time.deltaTime;
            if (timeSinceLastSend >= sendInterval && movementDirection != lastSentDirection)
            {
                MoveServerRpc(movementDirection.normalized, movementSpeed);
                lastSentDirection = movementDirection;
                timeSinceLastSend = 0f;
            }

            playerAnimator.Animate(movementDirection, movementSpeed);
        }

        [ServerRpc]
        private void MoveServerRpc(Vector2 direction, float speed)
        {
            speed = Mathf.Clamp(speed, 0f, 1f);
            playerRigidbody.linearVelocity = new Vector3(direction.x, 0, direction.y) * speed * movementComponent.BaseSpeed;

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
        public void Aim()
        {
            Vector2 aim = currentController.GetAim();
            if (aim == Vector2.zero)
            {
                crosshair.gameObject.SetActive(false);
                isAccurance = false;
                return;
            }
            SetAimAddition(aim);

            if (aim.magnitude < aimComponent.CrosshairDistance) crosshair.localPosition = aim;
            else crosshair.localPosition = aim.normalized * aimComponent.CrosshairDistance;
            crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            crosshair.gameObject.SetActive(true);
            isAccurance = true;
        }

        private void SetAimAddition(Vector3 curAim){
            
            float factMouseFactor = 1f;

            switch (gameSettings.controlType)
            {
                case ControlType.Personal:
                    factMouseFactor = 1f;
                    break;
                case ControlType.Mobile:
                    if(isAimMode) factMouseFactor = 0.2f;
                    break;
            }

            float factMouseMagnitute = curAim.magnitude;
            Vector3 factMousePosition = curAim.normalized;
            if (factMouseMagnitute > aimComponent.MaxCrosshairDistance * factMouseFactor) factMousePosition *= aimComponent.MaxCrosshairDistance;
            else if (factMouseMagnitute < aimComponent.CrosshairDistance * factMouseFactor + 1) factMousePosition = Vector2.zero;

            entityEventBus.Invoke(playerEntity, new AimAddition {Position = factMousePosition});
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
                int currentY = Mathf.RoundToInt(transform.position.y);
                for (int xOffset = -aimComponent.PickDistance; xOffset <= aimComponent.PickDistance; xOffset++)
                    for (int yOffset = -aimComponent.PickDistance; yOffset <= aimComponent.PickDistance; yOffset++)
                        entityEventBus.Invoke(playerEntity, new PickUpEvent { Position = new Vector2Int(currentX + xOffset, currentY + yOffset) });
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
                if (act) In().Forget();
            }
        }

        private async UniTaskVoid In()
        {
            try
            {
                act = false;
                entityEventBus.Invoke(playerEntity, new PlaceEvent { Position = Extension.RoundVector2D(crosshair.position) } );
                await UniTask.Delay(placeObjectDelay);
                act = true;
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error in In(): {ex.Message}");
            }
        }
        public void Delete()
        {
            currentController.MeetEnds();
            Raise.action.performed -= AimRaise;
            LeftClick.action.performed -= AimPlace;
        }

        public Transform GetModelTransform() => this.transform;

        public Vector2 GetEntityVelocity()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}