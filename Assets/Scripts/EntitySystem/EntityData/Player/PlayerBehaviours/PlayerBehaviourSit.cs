using System;

public class PlayerBehaviourSit : IPlayerBehaviour
{
    private IControllable controller;
    private Behaviour behaviourSit;

    public PlayerBehaviourSit(IControllable _controller, Action _delegateIdle){
        controller = _controller;
        behaviourSit = _delegateIdle.Invoke;
    }

    public void Enter()
    {
        // PlayerController.instance.reloadSpeed = 1.5f;
    }

    public void Exit()
    {

    }

    public void Update()
    {
        behaviourSit();
    }
}
