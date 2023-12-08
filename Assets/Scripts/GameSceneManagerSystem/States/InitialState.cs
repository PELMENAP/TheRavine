using UnityEngine;

public class InitialState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer { get; }

    private int timer = 0;
    private bool isChangeState = false, isLoad;

    public InitialState(Bootstrap initializer, bool _isLoad)
    {
        Initializer = initializer;
        isLoad = _isLoad;
    }

    public void OnEnter()
    {
        Initializer.StartNewServise(null);
        Initializer.StartNewServise(() => isChangeState = true);
        FaderOnTransit.instance.SetLogs("Загрузка ресурсов");
    }

    public void OnExit()
    {
        FaderOnTransit.instance.SetLogs("Загрузка ресурсов завершена");
    }

    public void OnTick()
    {
        timer += Initializer.tickPerUpdate;
        if (timer > 100 && isChangeState)
        {
            isChangeState = false;
            Initializer.StateMachine.SwitchState<LoadingState>();
        }
    }
}
