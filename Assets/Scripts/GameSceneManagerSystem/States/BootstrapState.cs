using UnityEngine;

public class BootstrapState : IState<Bootstrap>, IEnterable, IExitable
{
    public Bootstrap Initializer {get;}

    public BootstrapState(Bootstrap initializer){
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Debug.Log("Выполнен вход в игру");
        Debug.Log("Создание точки входа");
        Initializer.StateMachine.SwitchState<InitialState>();
    }

    public void OnExit(){
        Debug.Log("Точка входа создана");
    }
}
