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
        Initializer.result = false;
        Initializer.StartNewServise();
        FaderOnTransit.instance.SetLogs("Создание сцены");
    }

    public void OnExit()
    {
        FaderOnTransit.instance.SetLogs("Сцена создана");
    }

    public void OnTick()
    {

        timer += 1f;

        if (timer > 100f && !isChangeState && Initializer.result)
        {
            isChangeState = true;
            Initializer.StateMachine.SwitchState<GameState>();
        }

        if ((int)timer >= timerStep * 10 && !isChangeState)
        {
            FaderOnTransit.instance.SetLogs($"Загрузка сцены {timer}%");
            timerStep += 1;
        }
    }
}
