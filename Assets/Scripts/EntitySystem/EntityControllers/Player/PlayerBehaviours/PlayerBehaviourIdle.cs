using System;
public class PlayerBehaviourIdle : AState
{
    private IEntityControllable controller;
    private Behaviour behaviourIdle;
    public PlayerBehaviourIdle(IEntityControllable _controller, Action _delegateIdle)
    {
        controller = _controller;
        behaviourIdle = _delegateIdle.Invoke;
    }
    public override void Enter()
    {

    }

    public override void Exit()
    {
        CancelCurrentCommand();
    }

    public override void Update()
    {
        behaviourIdle();
    }
}
