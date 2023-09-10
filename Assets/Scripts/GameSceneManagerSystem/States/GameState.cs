using UnityEngine;

public class GameState : IState<Bootstrap>, IEnterable, IExitable
{
    public Bootstrap Initializer {get;}

    public GameState(Bootstrap initializer){
        Initializer = initializer;
    }

    public void OnEnter()
    {
        Debug.Log("Начало игры");
    }

    public void OnExit(){
        Debug.Log("Игра закончена");
    }

    // public void OnTick(){
        // if(Input.GetKeyDown("space")){

        // }
        
    // }
}
