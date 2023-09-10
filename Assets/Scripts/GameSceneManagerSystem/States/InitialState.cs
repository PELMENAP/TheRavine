using UnityEngine;

public class InitialState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer {get;}

    private float timer = 0;
    private int timerStep = 1;
    private bool isChangeState = false;

    public InitialState(Bootstrap initializer){
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Debug.Log("Загрузка ресурсов");
    }

    public void OnExit(){
        Debug.Log("Загрузка ресурсов завершена");
    }

    public void OnTick(){

        timer += 1f;

        if(timer > 100f && !isChangeState){
            Initializer.StateMachine.SwitchState<LoadingState>();
            isChangeState = true;
        }

        if((int)timer >= timerStep * 25 && !isChangeState){
            Debug.Log((int)timer);
            timerStep += 1;
        }
    }
}
