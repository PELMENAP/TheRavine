using UnityEngine;

public class InitialState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer { get; }

    private float timer = 0;
    private bool isChangeState = false;

    public InitialState(Bootstrap initializer)
    {
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Initializer.StartNewServise();
        FaderOnTransit.instance.SetLogs("Загрузка ресурсов");
    }

    public void OnExit()
    {
        FaderOnTransit.instance.SetLogs("Загрузка ресурсов завершена");
    }

    public void OnTick()
    {

        timer += 1f;
        if (Initializer.isTest)
        {
            timer += 1f;
        }

        if (timer > 100f && !isChangeState)
        {
            if (Initializer.isTest)
                Initializer.StateMachine.SwitchState<GameState>();
            Initializer.StateMachine.SwitchState<LoadingState>();
            isChangeState = true;
        }
    }
}
