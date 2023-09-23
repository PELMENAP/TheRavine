using UnityEngine;
using System;

public class JoistickController : MonoBehaviour
{
    [SerializeField] private Joystick joystick;
    private IControllable _controllable;

    private void Awake() {
        if(!Settings.isJoistick){
            this.enabled = false;
            return;}
        _controllable = GetComponent<IControllable>();

        joystick.gameObject.SetActive(true);
        if(_controllable == null)
            throw new Exception("There's no IControllable component");
    }

    private void FixedUpdate() {
        GetMove();
        GetJump();
    }

    private void GetMove(){
        Vector2 direction = new Vector2(joystick.Horizontal, joystick.Vertical);
        if (direction.magnitude < 0.5f)
            direction = Vector2.zero;

        _controllable.Move(direction);
    }

    private void GetJump(){

    }
}
