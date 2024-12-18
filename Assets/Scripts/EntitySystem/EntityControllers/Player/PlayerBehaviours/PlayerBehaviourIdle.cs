using System;
public class PlayerBehaviourIdle : AState
{
    private IEntityController controller;
    private Behaviour behaviourIdle;
    public PlayerBehaviourIdle(IEntityController _controller, Action _delegateIdle)
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
