using UnityEngine;

public class LoadingState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer {get;}

    private float timer = 0;
    private int timerStep = 1;
    private int step = 0;
    private bool isChangeState = false;

    public LoadingState(Bootstrap initializer){
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Debug.Log("Создание сцены");
    }

    public void OnExit(){
        Debug.Log("Сцена создана");
    }

    public void OnTick(){

        timer += 0.5f;

        if(timer > 100f && !isChangeState){
            isChangeState = true;
            Initializer.StateMachine.SwitchState<GameState>();
        }
        else{
            if(timer > 10f && step == 0){
                Debug.Log("Объект 1 создан");
                step += 1;
            }
            else if(timer > 30f && step == 1){
                Debug.Log("Объект 2 создан");
                step += 1;
            }
            else if(timer > 50f && step == 2){
                Debug.Log("Объект 3 создан");
                step += 1;
            }
        }

        if((int)timer >= timerStep * 25 && !isChangeState){
            Debug.Log((int)timer);
            timerStep += 1;
        }
    }
}
