using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    private readonly InputActionReference MovementRef, RightClick;
    private readonly Mouse mouse;
    private readonly Camera cam;
    private readonly Transform playerTrans;
    private bool aim;
    public PCController(InputActionReference _MovementRef, InputActionReference _RightClick, Camera _cam, Transform _playerTrans)
    {
        mouse = Mouse.current;
        MovementRef = _MovementRef;
        cam = _cam;
        playerTrans = _playerTrans;
        RightClick = _RightClick;
        RightClick.action.started += EnableAimDirection;
        RightClick.action.canceled += DisableAimDirection;
    }
    
    private void EnableAimDirection(InputAction.CallbackContext context) 
    {
        Debug.Log("disable aim");
        aim = true;
    }
    private void DisableAimDirection(InputAction.CallbackContext context) 
    {
        Debug.Log("disable aim");
        aim = false;
    }

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
