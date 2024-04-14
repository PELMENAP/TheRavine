using UnityEngine;

public class JoistickController : IController
{
    private Joystick joystick;

    public JoistickController(Joystick _joystick)
    {
        joystick = _joystick;
        joystick.Activate();
    }

    public Vector2 GetMove() => joystick.Movement;

    public Vector2 GetAim() => joystick.Aim;

    public float GetJump()
    {
        return 0f;
    }

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
    }
}
