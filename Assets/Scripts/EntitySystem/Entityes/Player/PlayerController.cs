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
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : NetworkBehaviour, IEntityController
    {
        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float movementMinimum, sendInterval = 0.1f;
        [SerializeField] private Animator animator, shadowAnimator;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Transform crosshair, playerMark;
        private Rigidbody2D rb;
        private float movementSpeed;
        private Vector2 movementDirection;
        private Vector2 lastSentDirection;
        private float timeSinceLastSend;
        private bool isAimMode;
        private bool act = true;
        private bool isAccurance;

        private IController currentController;
        private ILogger logger;
        private EventBusByName entityEventBus;
        private EntityAimBaseStats aimBaseStats;
        private EntityMovementBaseStats movementBaseStats;
        public void SetInitialValues(AEntity entity, ILogger logger)
        {
            this.logger = logger;
            this.transform.position = Extension.GetRandomPointAround(this.transform.position, 10);

            currentController = Settings._controlType switch
            {
                ControlType.Personal => new PCController(Movement, RightClick, transform),
                ControlType.Mobile => new JoistickController(joystick),
                _ => throw new System.NotImplementedException()
            };

            GetPlayerComponents(entity);

            Raise.action.performed += AimRaise;
            LeftClick.action.performed += AimPlace;
        }

        private void GetPlayerComponents(AEntity entity)
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            
            entityEventBus = entity.GetEntityComponent<EventBusComponent>().EventBus;
            movementBaseStats = entity.GetEntityComponent<MovementComponent>().baseStats;
            aimBaseStats = entity.GetEntityComponent<AimComponent>().BaseStats;
            InitStatePattern(entity.GetEntityComponent<StatePatternComponent>());
        }

        private void InitStatePattern(StatePatternComponent component)
        {
            Action actions = Move;
            actions += Animate;
            actions += Aim;
            component.AddBehaviour(typeof(PlayerBehaviourIdle), new PlayerBehaviourIdle (this, actions, logger));
            
            actions = Animate;
            actions += Aim;
            component.AddBehaviour(typeof(PlayerBehaviourSit), new PlayerBehaviourSit(this, actions));
        }

        public void SetZeroValues()
        {
            movementSpeed = 0f;
            // movementDirection = new Vector2(0, 0);
            Animate();
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
            movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0f, 1f);

            if (movementSpeed < movementMinimum) movementDirection = Vector2.zero;

            timeSinceLastSend += Time.deltaTime;
            if (timeSinceLastSend >= sendInterval && movementDirection != lastSentDirection)
            {
                MoveServerRpc(movementDirection.normalized, movementSpeed);
                lastSentDirection = movementDirection;
                timeSinceLastSend = 0f;
            }

            MoveMark();
            Animate();
        }

        [ServerRpc]
        private void MoveServerRpc(Vector2 direction, float speed)
        {
            // validation

            direction = direction.normalized;
            speed = Mathf.Clamp(speed, 0f, 1f);
            rb.velocity = direction * speed * movementBaseStats.baseSpeed;

            UpdateClientPositionClientRpc(rb.position, rb.velocity);
        }

        [ClientRpc]
        private void UpdateClientPositionClientRpc(Vector2 position, Vector2 velocity)
        {
            if (IsOwner) return;

            rb.position = position;
            rb.velocity = velocity;
        }

        private readonly Vector3 Offset = new(0, 0, 100);
        private void MoveMark()
        {
            playerMark.position = transform.position + Offset;
            if (movementSpeed > movementMinimum / 2)
            {
                float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90;
                playerMark.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private void Animate()
        {
            if (movementDirection != Vector2.zero)
            {
                animator.SetFloat("Horizontal", movementDirection.x);
                animator.SetFloat("Vertical", movementDirection.y);
                if (Settings.isShadow)
                {
                    shadowAnimator.SetFloat("Horizontal", movementDirection.x);
                    shadowAnimator.SetFloat("Vertical", movementDirection.y);
                }
            }

            animator.SetFloat("Speed", movementSpeed);
            if (Settings.isShadow)
                shadowAnimator.SetFloat("Speed", movementSpeed);
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

            if (aim.magnitude < aimBaseStats.crosshairDistance) crosshair.localPosition = aim;
            else crosshair.localPosition = aim.normalized * aimBaseStats.crosshairDistance;
            crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            crosshair.gameObject.SetActive(true);
            isAccurance = true;
        }

        private void SetAimAddition(Vector3 curAim){
            
            float factMouseFactor = 1f;

            switch (Settings._controlType)
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
            if (factMouseMagnitute > aimBaseStats.maxCrosshairDistance * factMouseFactor) factMousePosition *= aimBaseStats.maxCrosshairDistance;
            else if (factMouseMagnitute < aimBaseStats.crosshairDistance * factMouseFactor + 1) factMousePosition = Vector2.zero;

            entityEventBus.Invoke(nameof(AimAddition), factMousePosition);
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
                for (int xOffset = -aimBaseStats.pickDistance; xOffset <= aimBaseStats.pickDistance; xOffset++)
                    for (int yOffset = -aimBaseStats.pickDistance; yOffset <= aimBaseStats.pickDistance; yOffset++)
                        entityEventBus.Invoke(nameof(PickUpEvent), new Vector2(currentX + xOffset, currentY + yOffset));
            }
            else entityEventBus.Invoke(nameof(PickUpEvent), Extension.RoundVector2D(crosshair.position));
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
                entityEventBus.Invoke(nameof(PlaceEvent), Extension.RoundVector2D(crosshair.position));
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
    }
}