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
        FaderOnTransit.instance.SetLogs("Начало игры");
        FaderOnTransit.instance.SetLogs("");
        FaderOnTransit.instance.FadeOut(() => aboba = true);
    }

    public void OnExit()
    {
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
