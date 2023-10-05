using System;
using UnityEngine;

public class JoistickController : IController
{
    private Joystick joystick;
    private Vector2 direction;

    public JoistickController(Joystick _joystick) {
        joystick = _joystick;

        joystick.gameObject.SetActive(true);
    }

    public Vector2 GetMove(){
        direction = new Vector2(joystick.Horizontal, joystick.Vertical);
        if (direction.magnitude < 0.5f)
            direction = Vector2.zero;
        return direction;
    }

    public void GetJump(){

    }
}
