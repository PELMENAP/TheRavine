using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    private InputActionReference MovementRef, RightClick;
    private Mouse mouse;
    private Camera cam;
    private Transform playerTrans;
    private bool aim;
    public PCController(InputActionReference _MovementRef, InputActionReference _RightClick, Camera _cam, Transform _playerTrans)
    {
        mouse = Mouse.current;
        MovementRef = _MovementRef;
        cam = _cam;
        playerTrans = _playerTrans;
        RightClick = _RightClick;
        RightClick.action.performed += EnableAimDirection;
        RightClick.action.canceled += DisableAimDirection;
    }
    
    private void EnableAimDirection(InputAction.CallbackContext context) => aim = true;
    private void DisableAimDirection(InputAction.CallbackContext context) => aim = false;

    public Vector2 GetMove() => MovementRef.action.ReadValue<Vector2>();

    public Vector2 GetAim()
    {
        if (aim) return cam.ScreenToWorldPoint(mouse.position.ReadValue()) - playerTrans.position;
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
        RightClick.action.performed -= EnableAimDirection;
        RightClick.action.canceled -= DisableAimDirection;
    }

}
