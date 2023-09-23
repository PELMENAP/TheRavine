using UnityEngine;
using System;

public class PCController : MonoBehaviour
{
    private IControllable _controllable;
    private void Awake() {
        if(Settings.isJoistick){
            this.enabled = false;
            return;}
        _controllable = GetComponent<IControllable>();

        if(_controllable == null)
            throw new Exception("There's no IControllable component");
    }

    private void FixedUpdate() {
        GetMove();
        GetJump();
    }

    private void GetMove(){
        Vector2 direction = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        _controllable.Move(direction);
    }

    private void GetJump(){

    }

}
