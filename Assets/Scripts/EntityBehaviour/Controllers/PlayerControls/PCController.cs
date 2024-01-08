using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    private InputActionReference MovementRef;
    public PCController(InputActionReference _MovementRef)
    {
        MovementRef = _MovementRef;
    }

    public Vector2 GetMove() => MovementRef.action.ReadValue<Vector2>();


    public void GetJump()
    {

    }
    public void EnableView()
    {

    }
    public void DisableView()
    {

    }

    public void MeetEnds()
    {

    }

}
