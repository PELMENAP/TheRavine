using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Cysharp.Threading.Tasks;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.Events;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : NetworkBehaviour, IEntityController
    {
        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float movementMinimum;
        [SerializeField] private Animator animator, shadowAnimator;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Transform crosshair, playerMark;
        private EventBusByName entityEventBus;
        private EntityAimBaseStats aimBaseStats;
        private EntityMovementBaseStats movementBaseStats;
        private bool act = true;
        private Rigidbody2D rb;
        private float movementSpeed;
        public Vector2 movementDirection;
        private IController currentController;
        public void SetInitialValues(AEntity entity)
        {
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
            rb = (Rigidbody2D)GetComponent("Rigidbody2D");
            rb.bodyType = RigidbodyType2D.Dynamic;
            
            entityEventBus = entity.GetEntityComponent<EventBusComponent>().EventBus;
            movementBaseStats = entity.GetEntityComponent<MovementComponent>().baseStats;
            aimBaseStats = entity.GetEntityComponent<AimComponent>().BaseStats;
            InitStatePattern(entity.GetEntityComponent<StatePatternComponent>());
        }

        private void InitStatePattern(StatePatternComponent component)
        {
            System.Action actions = Move;
            actions += Animate;
            actions += Aim;
            component.AddBehaviour(typeof(PlayerBehaviourIdle), new PlayerBehaviourIdle (this, actions));
            
            // actions = Animate;
            // actions += Aim;
            // PlayerBehaviourDialogue Dialogue = new PlayerBehaviourDialogue();
            // component.AddBehaviour(typeof(PlayerBehaviourDialogue), Dialogue);

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
            if(isAimMode || !IsOwner) return;
            movementDirection = currentController.GetMove();
            movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0.0f, 1.0f);

            if (movementDirection.magnitude < movementMinimum) movementDirection = Vector2.zero;
            
            MoveServerRpc(movementDirection, movementSpeed);
            MoveMark();
        }

        [ServerRpc]
        private void MoveServerRpc(Vector2 direction, float speed)
        {
            rb.velocity = movementBaseStats.baseSpeed * speed * direction;
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
            if (movementSpeed > movementMinimum / 2) playerMark.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90);
        }

        public void Animate()
        {
            if (IsOwner) PlayAnimationServerRpc();
        }

        [ServerRpc]
        void PlayAnimationServerRpc()
        {
            PlayAnimationClientRpc();
        }

        [ClientRpc]
        void PlayAnimationClientRpc()
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
            if (Settings.isShadow) shadowAnimator.SetFloat("Speed", movementSpeed);
        }
        private bool isAccurance;
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

        private bool isAimMode = false;

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
            act = false;

            entityEventBus.Invoke(nameof(PlaceEvent), Extension.RoundVector2D(crosshair.position));
            await UniTask.Delay(placeObjectDelay);
            act = true;
        }
        public void Delete()
        {
            currentController.MeetEnds();
            Raise.action.performed -= AimRaise;
            LeftClick.action.performed -= AimPlace;
        }
    }
}