using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

using TheRavine.EntityControl;

public class BotController : MonoBehaviour
{
    private const int PickDistance = 1;
    [SerializeField] private float movementSpeed, offset, timeLimit;
    [SerializeField] private Vector2 movementDirection, aim;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator, shadowAnimator;
    [SerializeField] private InputActionReference MovementRef;
    private IController currentController;
    private Transform crosshair, entityTrans, playerMark;
    public void SetInitialValues(AEntity entity, RavineLogger logger)
    {
    }
    public void EnableComponents()
    {
    }

    public void SetZeroValues()
    {
        movementDirection = new Vector2(0, 0);
        Animate();
    }
    public void DisableComponents()
    {
    }

    public void Move()
    {
    }

    public void Jump()
    {

    }

    private void MoveMark()
    {
    }

    public void Animate()
    {

    }

    public void Aim()
    {
    }


    public void Delete() { }
    public Transform GetModelTransform() => this.transform;
    public Vector2 GetEntityVelocity()
    {
        throw new NotImplementedException();
    }
}
