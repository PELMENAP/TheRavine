using UnityEngine;

public class GameState : IState<Bootstrap>, IEnterable, IExitable
{
    public Bootstrap Initializer { get; }
    bool aboba = false;

    public GameState(Bootstrap initializer)
    {
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Initializer.result = false;
        Initializer.StartNewServise();
        FaderOnTransit.instance.SetLogs("Начало игры");
        FaderOnTransit.instance.FadeOut(() => aboba = true);
        Initializer.Finally();
    }

    public void OnExit()
    {
        DayCycle.closeThread = false;
        Debug.Log("Игра закончена");
    }

    public void OnTick()
    {
        if (Input.GetKeyDown("space"))
        {
            Debug.Log("игровой тик");
        }

        if (aboba)
        {
            // Initializer.DestroySceneTransitions();
            aboba = false;
        }
    }
}
