using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

using TheRavine.Extentions;
using TheRavine.Base;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour, IControllable
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

    public void SetInitialValues()
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
        Raise.action.performed += AimRaise;
        LeftClick.action.performed += AimPlace;
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
        rb.velocity = movementDirection * movementSpeed * PlayerEntity.data.MOVEMENT_BASE_SPEED;
        MoveMark();
    }

    public void Jump()
    {

    }

    private void MoveMark()
    {
        playerMark.position = playerTrans.position - new Vector3(0, 0, -100);
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
        PlayerEntity.data.setMouse?.Invoke(aim);
        crosshair.localPosition = aim * PlayerEntity.data.CROSSHAIR_DISTANSE; ;
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
    public void BreakUp()
    {
        currentController.MeetEnds();
        Raise.action.performed -= AimRaise;
        LeftClick.action.performed -= AimPlace;
    }
}