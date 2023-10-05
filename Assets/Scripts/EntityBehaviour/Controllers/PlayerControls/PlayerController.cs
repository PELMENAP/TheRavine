using UnityEngine;
using System.Collections;
using System;

public class PlayerController : MonoBehaviour, IControllable
{
    [SerializeField] private float MOVEMENT_BASE_SPEED, movementSpeed, CROSSHAIR_DISTANSE, offset, timeLimit;
    [SerializeField] private Vector2 movementDirection, aim;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator, shadowAnimator;
    private IController currentController;
    private bool act = true;
    private Transform crosshair, entityTrans, playerMark;

    public void SetInitialValues(){
        rb = (Rigidbody2D)this.GetComponent("Rigidbody2D");
        entityTrans = PlayerData.instance.entityTrans;
        crosshair = PlayerData.instance.crosshair;
        playerMark = PlayerData.instance.playerMark;
        switch (Settings._controlType)
        {
            case ControlType.Personal:
                currentController = new PCController();
                break;
            case ControlType.Mobile:
                currentController = new JoistickController(PlayerData.instance.joystick);
                break;
        }
    }

    public void SetZeroValues(){
        movementSpeed = 0f;
        movementDirection = new Vector2(0, 0);
        Animate();
    }

    public void Move(){
        movementDirection = currentController.GetMove();
        movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0.0f, 1.0f);
        movementDirection.Normalize();
        rb.velocity = movementDirection * movementSpeed * MOVEMENT_BASE_SPEED;
        MoveMark();
    }

    public void Jump(){

    }

    private void MoveMark()
    {
        playerMark.position = new Vector3(entityTrans.position.x, entityTrans.position.y, 99);
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

        if(Settings.isShadow){
            if (movementDirection != Vector2.zero)
            {
                shadowAnimator.SetFloat("Horizontal", movementDirection.x);
                shadowAnimator.SetFloat("Vertical", movementDirection.y);
            }
            shadowAnimator.SetFloat("Speed", movementSpeed);
        }
    }

    public void Aim()
    {
        if (Input.GetMouseButton(1))
        {
            aim = PlayerData.instance.cachedCamera.ScreenToWorldPoint(Input.mousePosition) - entityTrans.position;
            if (aim.magnitude > 2)
            {
                aim.Normalize();
                crosshair.localPosition = aim * CROSSHAIR_DISTANSE;
            }
            else
                crosshair.localPosition = aim;
            crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + offset);
            crosshair.gameObject.SetActive(true);
            AimPlace();
            AimRaise(crosshair.position);
        }
        else
        {
            crosshair.gameObject.SetActive(false);
            AimRaise(entityTrans.position);
        }
    }

    private void AimRaise(Vector3 position){
        if(Input.GetKeyDown("f")){
            PlayerData.instance.aimRaise?.Invoke(new Vector3((int)Math.Round(position.x), (int)Math.Round(position.y), 0));
        }
    }

    private void AimPlace()
    {
        if (Input.GetMouseButton(0))
        {
            if (act)
                StartCoroutine(In());
        }
    }

    private IEnumerator In()
    {
        act = false;
        PlayerData.instance.placeObject?.Invoke(new Vector3((int)Math.Round(crosshair.position.x), (int)Math.Round(crosshair.position.y), 0));
        yield return new WaitForSeconds(timeLimit);
        act = true;
    }
}
