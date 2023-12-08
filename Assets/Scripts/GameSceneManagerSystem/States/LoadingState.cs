using UnityEngine;

public class LoadingState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer { get; }
    private float timer = 0;
    private int timerStep = 1;
    private bool isChangeState = false;

    public LoadingState(Bootstrap initializer)
    {
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Initializer.StartNewServise(() => isChangeState = true);
        FaderOnTransit.instance.SetLogs("Создание сцены");
    }

    public void OnExit()
    {
        FaderOnTransit.instance.SetLogs("Сцена создана");
    }

    public void OnTick()
    {
        timer += Initializer.tickPerUpdate;
        if (timer > 100 && isChangeState)
        {
            isChangeState = false;
            Initializer.StateMachine.SwitchState<GameState>();
        }

        if (timer >= timerStep * 10)
        {
            FaderOnTransit.instance.SetLogs($"Загрузка сцены {timer}%");
            timerStep += 1;
        }
    }
}
