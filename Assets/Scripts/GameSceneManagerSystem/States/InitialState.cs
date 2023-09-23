using UnityEngine;

public class InitialState : IState<Bootstrap>, IEnterable, IExitable, ITickable
{
    public Bootstrap Initializer { get; }

    private float timer = 0;
    private int timerStep = 1;
    private bool isChangeState = false;

    public InitialState(Bootstrap initializer)
    {
        Initializer = initializer;
    }

    public void OnEnter()
    {
        FaderOnTransit.instance.SetLogs("Загрузка ресурсов");
    }

    public void OnExit()
    {
        FaderOnTransit.instance.SetLogs("Загрузка ресурсов завершена");
    }

    public void OnTick()
    {

        timer += 1f;

        if (timer > 100f && !isChangeState)
        {
            Initializer.StateMachine.SwitchState<LoadingState>();
            isChangeState = true;
        }

        if ((int)timer >= timerStep * 10 && !isChangeState)
        {
            Initializer.InstantiateRequiredPrefab(timerStep - 1);
            FaderOnTransit.instance.SetLogs($"Объект {timerStep} создан");
            timerStep += 1;
        }
    }
}
