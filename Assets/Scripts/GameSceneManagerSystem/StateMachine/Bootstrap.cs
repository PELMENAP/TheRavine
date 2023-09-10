using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    public StateMachine<Bootstrap> StateMachine{get; private set;}

    private void Awake() {
        StateMachine = new StateMachine<Bootstrap>(
            new BootstrapState(this),
            new InitialState(this),
            new LoadingState(this),
            new GameState(this)
        );
    }

    private void Update() {
        if(Input.GetKeyDown("space")){
            StateMachine.SwitchState<BootstrapState>();
        }
    }
}
