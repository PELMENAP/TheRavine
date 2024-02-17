using System;
using UnityEngine;

public class JoistickController : IController
{
    private Joystick joystick;
    private Vector2 direction;

    public JoistickController(Joystick _joystick)
    {
        joystick = _joystick;
    }

    public Vector2 GetMove() => joystick.Direction;

    public Vector2 GetAim() => joystick.Direction;

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
