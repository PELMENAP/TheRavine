using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

public class JoistickController : IController
{
    private readonly Joystick joystick;
    public JoistickController(Joystick _joystick)
    {
        EnhancedTouchSupport.Enable();
        joystick = _joystick;
        joystick.Activate();
    }
    public Vector2 GetMove() => joystick.Movement;
    public Vector2 GetAim() => joystick.Aim;
    public void EnableView()
    {
        joystick.gameObject.SetActive(true);
    }
    public void DisableView()
    {
        joystick.gameObject.SetActive(false);
    }
    public void MeetEnds()
    {
        joystick.OnDisabling();
        EnhancedTouchSupport.Disable();
    }
}
