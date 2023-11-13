using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    public PCController()
    {

    }

    public Vector2 GetMove() => GData.GInput.Gameplay.Movement.ReadValue<Vector2>();


    public void GetJump()
    {

    }

    public void MeetEnds()
    {

    }

}
