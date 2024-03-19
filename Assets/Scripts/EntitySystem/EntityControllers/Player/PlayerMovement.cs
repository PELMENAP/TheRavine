using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

using TheRavine.Extentions;
using TheRavine.Base;
using TheRavine.EntityControl;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour, IEntityControllable
{
    private const int PickDistance = 1;
    [SerializeField] private float movementSpeed, offset, timeLimit;
    [SerializeField] private Vector2 movementDirection, aim;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator, shadowAnimator;
    [SerializeField] private InputActionReference Movement, Raise, RightClick, LeftClick;
    [SerializeField] private Joystick joystick;
    [SerializeField] private Camera cachedCamera;
    private IController currentController;
    private bool act = true;
    [SerializeField] private Transform crosshair, playerTrans, playerMark;
    private EntityAimBaseStats aimBaseStats;
    private EntityMovementBaseStats movementBaseStats;

    public void SetInitialValues(AEntity entity)
    {
        rb = (Rigidbody2D)this.GetComponent("Rigidbody2D");
        switch (Settings._controlType)
        {
            case ControlType.Personal:
                currentController = new PCController(Movement, RightClick, cachedCamera);
                break;
            case ControlType.Mobile:
                currentController = new JoistickController(joystick);
                break;
        }
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
        movementDirection = currentController.GetMove();
        if (movementDirection.magnitude < 0.6f)
            movementDirection = Vector2.zero;
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
        playerMark.position = playerTrans.position + Offset;
        if (movementSpeed > 0.5f)
            playerMark.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90);
    }

    public void Animate()
    {
        if (movementDirection != Vector2.zero)
        {
            animator.SetFloat("Horizontal", movementDirection.x);
            animator.SetFloat("Vertical", movementDirection.y);
        }
        animator.SetFloat("Speed", movementSpeed);

        if (Settings.isShadow)
        {
            if (movementDirection != Vector2.zero)
            {
                shadowAnimator.SetFloat("Horizontal", movementDirection.x);
                shadowAnimator.SetFloat("Vertical", movementDirection.y);
            }
            shadowAnimator.SetFloat("Speed", movementSpeed);
        }
    }
    private bool isAccurance;
    public void Aim()
    {
        aim = currentController.GetAim();
        if (aim == Vector2.zero)
        {
            crosshair.gameObject.SetActive(false);
            isAccurance = false;
            return;
        }


        // PlayerEntity.data.setMouse?.Invoke(aim);

        Vector2 factMousePosition = new Vector2(aim.x, aim.y);
        if (factMousePosition.magnitude > aimBaseStats.maxCrosshairDistanse)
            factMousePosition = factMousePosition.normalized * aimBaseStats.maxCrosshairDistanse;
        if (factMousePosition.magnitude < aimBaseStats.crosshairDistanse + 1)
            factMousePosition = Vector2.zero;
        // factMousePosition = aim;  Invoke event

        // switch (Settings._controlType)
        // {
        //     case ControlType.Personal:
        //         if (Mouse.current.rightButton.isPressed)
        //             targetPos += PData.factMousePosition;
        //         break;
        //     case ControlType.Mobile:
        //         targetPos += PData.factMousePosition;
        //         break;
        // }

        crosshair.localPosition = aim * aimBaseStats.crosshairDistanse;
        crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + offset);
        crosshair.gameObject.SetActive(true);
        isAccurance = true;
    }

    public void AimRaiseMobile()
    {
        AimRaise(new InputAction.CallbackContext());
    }

    private void AimRaise(InputAction.CallbackContext obj)
    {
        if (!isAccurance)
        {
            int currentX = Mathf.RoundToInt(playerTrans.position.x);
            int currentY = Mathf.RoundToInt(playerTrans.position.y);
            for (int xOffset = -PickDistance; xOffset <= PickDistance; xOffset++)
                for (int yOffset = -PickDistance; yOffset <= PickDistance; yOffset++)
                    PlayerEntity.data.aimRaise?.Invoke(new Vector2(currentX + xOffset, currentY + yOffset));
        }
        else
        {
            PlayerEntity.data.aimRaise?.Invoke(Extention.RoundVector2D(crosshair.position));
        }
    }

    public void AimPlaceMobile()
    {
        AimPlace(new InputAction.CallbackContext());
    }

    private void AimPlace(InputAction.CallbackContext obj)
    {
        if (aim == Vector2.zero)
        {
            if (act)
                StartCoroutine(In());
        }
    }

    private IEnumerator In()
    {
        act = false;
        PlayerEntity.data.placeObject?.Invoke(Extention.RoundVector2D(crosshair.position));
        yield return new WaitForSeconds(timeLimit);
        act = true;
    }
    public void Delete()
    {
        currentController.MeetEnds();
        Raise.action.performed -= AimRaise;
        LeftClick.action.performed -= AimPlace;
    }
}
