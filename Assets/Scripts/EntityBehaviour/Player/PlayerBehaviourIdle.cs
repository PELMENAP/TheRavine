using UnityEngine;
public class PlayerBehaviourIdle : IPlayerBehaviour
{
    public Behaviour behaviourIdle;

    public void Enter()
    {
        PlayerController.instance.reloadSpeed = 1f;
    }

    public void Exit()
    {

    }

    public void Update()
    {
        behaviourIdle();
    }
}
