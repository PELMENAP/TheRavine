using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

public class PlayerController : MonoBehaviour, IControllable
{
    private const int PickDistance = 1;
    [SerializeField] private float movementSpeed, offset, timeLimit;
    [SerializeField] private Vector2 movementDirection, aim;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator, shadowAnimator;
    [SerializeField] private InputActionReference MovementRef, Raise;
    [SerializeField] private Joystick joystick;
    [SerializeField] private Camera cachedCamera;
    private IController currentController;
    private bool act = true;
    [SerializeField] private Transform crosshair, entityTrans, playerMark;

    public void SetInitialValues()
    {
        rb = (Rigidbody2D)this.GetComponent("Rigidbody2D");
        switch (Settings._controlType)
        {
            case ControlType.Personal:
                currentController = new PCController(MovementRef);
                break;
            case ControlType.Mobile:
                currentController = new JoistickController(joystick);
                break;
        }
        Raise.action.performed += AimRaise;
    }

    public void SetZeroValues()
    {
        movementSpeed = 0f;
        movementDirection = new Vector2(0, 0);
        Animate();
    }

    public void Move()
    {
        movementDirection = currentController.GetMove();
        movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0.0f, 1.0f);
        movementDirection.Normalize();
        rb.velocity = movementDirection * movementSpeed * PlayerData.data.MOVEMENT_BASE_SPEED;
        MoveMark();
    }

    public void Jump()
    {

    }

    private void MoveMark()
    {
        playerMark.position = entityTrans.position - new Vector3(0, 0, -100);
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
        if (Mouse.current.rightButton.isPressed)
        {
            aim = cachedCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - entityTrans.position;
            PlayerData.data.setMouse?.Invoke(new Vector3(aim.x, aim.y, 0));
            if (aim.magnitude > 2)
            {
                aim.Normalize();
                crosshair.localPosition = aim * PlayerData.data.CROSSHAIR_DISTANSE;
            }
            else
                crosshair.localPosition = aim;
            crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + offset);
            crosshair.gameObject.SetActive(true);
            AimPlace();
            isAccurance = true;
        }
        else
        {
            crosshair.gameObject.SetActive(false);
            isAccurance = false;
        }
    }

    private void AimRaise(InputAction.CallbackContext obj)
    {
        if (!isAccurance)
        {
            int currentX = Mathf.RoundToInt(entityTrans.position.x);
            int currentY = Mathf.RoundToInt(entityTrans.position.y);
            for (int xOffset = -PickDistance; xOffset <= PickDistance; xOffset++)
                for (int yOffset = -PickDistance; yOffset <= PickDistance; yOffset++)
                    PlayerData.data.aimRaise?.Invoke(new Vector2(currentX + xOffset, currentY + yOffset));
        }
        else
        {
            PlayerData.data.aimRaise?.Invoke(new Vector2(Mathf.RoundToInt(crosshair.position.x), Mathf.RoundToInt(crosshair.position.y)));
        }
    }

    private void AimPlace()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (act)
                StartCoroutine(In());
        }
    }

    private IEnumerator In()
    {
        act = false;
        PlayerData.data.placeObject?.Invoke(new Vector2(Mathf.RoundToInt(crosshair.position.x), Mathf.RoundToInt(crosshair.position.y)));
        yield return new WaitForSeconds(timeLimit);
        act = true;
    }
    public void BreakUp()
    {
        currentController.MeetEnds();
        Raise.action.performed -= AimRaise;
    }
    private void OnDisable()
    {
        BreakUp();
    }
}
