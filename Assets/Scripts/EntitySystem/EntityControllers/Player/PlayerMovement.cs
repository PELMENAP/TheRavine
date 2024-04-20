using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;

using TheRavine.Extentions;
using TheRavine.Base;
using TheRavine.Events;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour, IEntityControllable
    {
        private const int PickDistance = 1;
        [SerializeField] private int placeObjectDelay;
        [SerializeField] private float movementMinimum;
        [SerializeField] private Animator animator, shadowAnimator;
        [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick;
        [SerializeField] private Joystick joystick;
        [SerializeField] private Camera cachedCamera;
        [SerializeField] private Transform crosshair, playerMark;
        private EventBusByName entityEventBus;
        private EntityAimBaseStats aimBaseStats;
        private EntityMovementBaseStats movementBaseStats;
        private IController currentController;
        private bool act = true;
        private Rigidbody2D rb;
        private float movementSpeed;
        private Vector2 movementDirection;

        public void SetInitialValues(AEntity entity)
        {
            rb = (Rigidbody2D)this.GetComponent("Rigidbody2D");
            switch (Settings._controlType)
            {
                case ControlType.Personal:
                    currentController = new PCController(Movement, RightClick, cachedCamera, this.transform);
                    break;
                case ControlType.Mobile:
                    currentController = new JoistickController(joystick);
                    break;
            }
            entityEventBus = entity.GetEntityComponent<EventBusComponent>().EventBus;
            movementBaseStats = entity.GetEntityComponent<MovementComponent>().baseStats;
            aimBaseStats = entity.GetEntityComponent<AimComponent>().baseStats;
            InitStatePattern(entity.GetEntityComponent<StatePatternComponent>());
            Raise.action.performed += AimRaise;
            LeftClick.action.performed += AimPlace;
        }

        private void InitStatePattern(StatePatternComponent component)
        {
            System.Action actions = Move;
            actions += Animate;
            actions += Aim;
            PlayerBehaviourIdle Idle = new PlayerBehaviourIdle(this, actions);
            component.AddBehaviour(typeof(PlayerBehaviourIdle), Idle);
            // actions = Animate;
            // actions += Aim;
            // PlayerBehaviourDialoge Dialoge = new PlayerBehaviourDialoge();
            // component.AddBehaviour(typeof(PlayerBehaviourDialoge), Dialoge);
            actions = Animate;
            actions += Aim;
            PlayerBehaviourSit Sit = new PlayerBehaviourSit(this, actions);
            component.AddBehaviour(typeof(PlayerBehaviourSit), Sit);
        }

        public void SetZeroValues()
        {
            movementSpeed = 0f;
            movementDirection = new Vector2(0, 0);
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
            if(isAimMode) return;
            movementDirection = currentController.GetMove();
            if (movementDirection.magnitude < movementMinimum) movementDirection = Vector2.zero;
            movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0.0f, 1.0f);
            movementDirection.Normalize();
            rb.velocity = movementDirection * movementSpeed * movementBaseStats.baseSpeed;
            MoveMark();
        }

        public void Jump()
        {

        }

        private static readonly Vector3 Offset = new Vector3(0, 0, 100);
        private void MoveMark()
        {
            playerMark.position = this.transform.position + Offset;
            if (movementSpeed > 0.5f) playerMark.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90);
        }

        public void Animate()
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
        [SerializeField] private Vector2 aim;
        public void Aim()
        {
            aim = currentController.GetAim();
            if (aim == Vector2.zero)
            {
                crosshair.gameObject.SetActive(false);
                isAccurance = false;
                return;
            }
            SetAimAddition(aim);

            if (aim.magnitude < aimBaseStats.crosshairDistanse) crosshair.localPosition = aim;
            else crosshair.localPosition = aim.normalized * aimBaseStats.crosshairDistanse;
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
            if (factMouseMagnitute > aimBaseStats.maxCrosshairDistanse * factMouseFactor) factMousePosition *= aimBaseStats.maxCrosshairDistanse;
            else if (factMouseMagnitute < aimBaseStats.crosshairDistanse * factMouseFactor + 1) factMousePosition = Vector2.zero;

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
                int currentX = Mathf.RoundToInt(this.transform.position.x);
                int currentY = Mathf.RoundToInt(this.transform.position.y);
                for (int xOffset = -PickDistance; xOffset <= PickDistance; xOffset++)
                    for (int yOffset = -PickDistance; yOffset <= PickDistance; yOffset++)
                        entityEventBus.Invoke(nameof(RaiseEvent), new Vector2(currentX + xOffset, currentY + yOffset));
            }
            else entityEventBus.Invoke(nameof(RaiseEvent), Extention.RoundVector2D(crosshair.position));
        }

        public void AimPlaceMobile()
        {
            AimPlace(new InputAction.CallbackContext());
        }

        private void AimPlace(InputAction.CallbackContext obj)
        {
            // if (aim == Vector2.zero)
            // {
                if (act) In().Forget();
            // }
        }

        private async UniTaskVoid In()
        {
            act = false;

            entityEventBus.Invoke(nameof(PlaceEvent), Extention.RoundVector2D(crosshair.position));
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