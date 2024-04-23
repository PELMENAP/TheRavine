using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    private readonly InputActionReference MovementRef, RightClick;
    private readonly Mouse mouse;
    private readonly Camera cam;
    private readonly Transform playerTransform;

    public PCController(InputActionReference _MovementRef, InputActionReference _RightClick, Camera _cam, Transform _playerTransform)
    {
        mouse = Mouse.current;
        cam = _cam;
        MovementRef = _MovementRef;
        RightClick = _RightClick;
        playerTransform = _playerTransform;
    }
    public Vector2 GetMove() => MovementRef.action.ReadValue<Vector2>();

    public Vector2 GetAim()
    {
        if (RightClick.action.IsPressed()) return cam.ScreenToWorldPoint(mouse.position.ReadValue()) - playerTransform.position;
        return Vector2.zero;
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
