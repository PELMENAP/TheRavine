using System;
public class PlayerBehaviourIdle : IPlayerBehaviour
{
    private IControllable controller;
    private Behaviour behaviourIdle;

    public PlayerBehaviourIdle(IControllable _controller, Action _delegateIdle){
        controller = _controller;
        behaviourIdle = _delegateIdle.Invoke;
    }

    public void Enter()
    {
    }

    public void Exit()
    {
        controller.SetZeroValues();
    }

    public void Update()
    {
        behaviourIdle();
    }
}
