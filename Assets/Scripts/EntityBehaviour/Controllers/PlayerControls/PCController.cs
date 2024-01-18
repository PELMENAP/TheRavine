using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    private InputActionReference MovementRef, RightClick;
    private Mouse mouse;
    private Camera cam;
    public PCController(InputActionReference _MovementRef, InputActionReference _RightClick, Camera _cam)
    {
        mouse = Mouse.current;
        MovementRef = _MovementRef;
        cam = _cam;
        RightClick = _RightClick;
    }

    public Vector2 GetMove() => MovementRef.action.ReadValue<Vector2>();

    public Vector2 GetAim()
    {
        if (RightClick.action.triggered)
            return cam.ScreenToWorldPoint(mouse.position.ReadValue()).normalized;
        return Vector2.zero;
    }
    public float GetJump()
    {
        return 0f;
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
