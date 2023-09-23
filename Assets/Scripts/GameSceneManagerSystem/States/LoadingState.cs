using UnityEngine;

public class LoadingState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer { get; }

    private float timer = 0;
    private int timerStep = 1;
    private bool isChangeState = false, skipLoading;

    public LoadingState(Bootstrap initializer, bool _skipLoading = false)
    {
        Initializer = initializer;
        skipLoading = _skipLoading;
    }

    public void OnEnter()
    {
        FaderOnTransit.instance.SetLogs("Создание сцены");
        if(skipLoading)
            Initializer.StateMachine.SwitchState<GameState>();
    }

    public void OnExit()
    {
        FaderOnTransit.instance.SetLogs("Сцена создана");
    }

    public void OnTick()
    {

        timer += 1f;

        if (timer > 100f && !isChangeState)
        {
            isChangeState = true;
            Initializer.StateMachine.SwitchState<GameState>();
        }

        if ((int)timer >= timerStep * 10 && !isChangeState)
        {
            FaderOnTransit.instance.SetLogs($"Загрузка сцены {timer}%");
            timerStep += 1;
            if (!Settings.isLoad)
            {
                Initializer.StateMachine.SwitchState<GameState>();
                FaderOnTransit.instance.SetLogs("Загрузка сцены 100%");
            }
        }
    }
}
