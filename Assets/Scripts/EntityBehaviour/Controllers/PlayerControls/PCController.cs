using System;
using UnityEngine;

public class PCController : IController
{
    public PCController() {
        
    }

    public Vector2 GetMove(){
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    public void GetJump(){

    }

}
