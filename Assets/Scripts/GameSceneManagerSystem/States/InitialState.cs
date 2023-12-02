using UnityEngine;

public class InitialState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer { get; }

    private float timer = 0;
    private bool isChangeState = false, isLoad;

    public InitialState(Bootstrap initializer, bool _isLoad)
    {
        Initializer = initializer;
        isLoad = _isLoad;
    }

    public void OnEnter()
    {
        Initializer.result = false;
        Initializer.StartNewServise();
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
        if (timer > 100f && !isChangeState && Initializer.result)
        {
            Initializer.StateMachine.SwitchState<LoadingState>();
            isChangeState = true;
        }
    }
}
