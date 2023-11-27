using UnityEngine;

public class BootstrapState : IState<Bootstrap>, IEnterable, IExitable
{
    public Bootstrap Initializer { get; }

    public BootstrapState(Bootstrap initializer)
    {
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Initializer.StartNewServise();
        FaderOnTransit.instance.SetLogs("Выполнен вход в игру");
        FaderOnTransit.instance.SetLogs("Создание точки входа");
        Initializer.StateMachine.SwitchState<InitialState>();
    }

    public void OnExit()
    {
        FaderOnTransit.instance.SetLogs("Точка входа создана");
        if (!Initializer.isTest)
            Initializer.AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
    }
}
