using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

public class BotController : MonoBehaviour, IControllable
{
    private const int PickDistance = 1;
    [SerializeField] private float movementSpeed, offset, timeLimit;
    [SerializeField] private Vector2 movementDirection, aim;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator, shadowAnimator;
    [SerializeField] private InputActionReference MovementRef;
    private IController currentController;
    private Transform crosshair, entityTrans, playerMark;
    public void SetInitialValues()
    {
        // rb = (Rigidbody2D)this.GetComponent("Rigidbody2D");
        // entityTrans = PlayerData.instance.entityTrans;
        // crosshair = PlayerData.instance.crosshair;
        // playerMark = PlayerData.instance.playerMark;
        // switch (Settings._controlType)
        // {
        //     case ControlType.Personal:
        //         currentController = new PCController(MovementRef);
        //         break;
        //     case ControlType.Mobile:
        //         currentController = new JoistickController(PlayerData.instance.joystick);
        //         break;
        // }
    }
    public void EnableComponents()
    {
    }

    public void SetZeroValues()
    {
        //movementSpeed = 0f;
        movementDirection = new Vector2(0, 0);
        Animate();
    }
    public void DisableComponents()
    {
    }

    public void Move()
    {
        // movementDirection = currentController.GetMove();
        // movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0.0f, 1.0f);
        // movementDirection.Normalize();
        // rb.velocity = movementDirection * movementSpeed * PlayerData.instance.MOVEMENT_BASE_SPEED;
        // MoveMark();
    }

    public void Jump()
    {

    }

    private void MoveMark()
    {
        // playerMark.position = entityTrans.position;
        // if (movementSpeed > 0.5f)
        //     playerMark.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90);
    }

    public void Animate()
    {
        // if (movementDirection != Vector2.zero)
        // {
        //     animator.SetFloat("Horizontal", movementDirection.x);
        //     animator.SetFloat("Vertical", movementDirection.y);
        // }
        // animator.SetFloat("Speed", movementSpeed);

        // if (Settings.isShadow)
        // {
        //     if (movementDirection != Vector2.zero)
        //     {
        //         shadowAnimator.SetFloat("Horizontal", movementDirection.x);
        //         shadowAnimator.SetFloat("Vertical", movementDirection.y);
        //     }
        //     shadowAnimator.SetFloat("Speed", movementSpeed);
        // }
    }

    public void Aim()
    {
        // if (Mouse.current.rightButton.isPressed)
        // {
        //     aim = PlayerData.instance.cachedCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - entityTrans.position;
        //     PlayerData.instance.SetMousePosition(new Vector3(aim.x, aim.y, 0));
        //     if (aim.magnitude > 2)
        //     {
        //         aim.Normalize();
        //         crosshair.localPosition = aim * PlayerData.instance.CROSSHAIR_DISTANSE;
        //     }
        //     else
        //         crosshair.localPosition = aim;
        //     crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + offset);
        //     crosshair.gameObject.SetActive(true);
        //     AimPlace();
        //     AimRaise(crosshair.position, true);
        // }
        // else
        // {
        //     crosshair.gameObject.SetActive(false);
        //     AimRaise(entityTrans.position, false);
        // }
    }


    // private IEnumerator Placing()
    // {
    //     aim = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
    //     aim.Normalize();
    //     crosshair.gameObject.SetActive(true);
    //     crosshair.localPosition = aim * CROSSHAIR_DISTANSE;
    //     crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
    //     yield return new WaitForSeconds(1);
    //     Instantiate(plob, crosshair.position, Quaternion.identity);
    //     crosshair.gameObject.SetActive(false);
    // }
}
