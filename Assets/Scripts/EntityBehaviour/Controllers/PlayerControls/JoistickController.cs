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

    public Vector2 GetMove()
    {
        direction = new Vector2(joystick.Horizontal, joystick.Vertical);
        if (direction.magnitude < 0.5f)
            direction = Vector2.zero;
        return direction;
    }

    public void GetJump()
    {

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

    }
}
