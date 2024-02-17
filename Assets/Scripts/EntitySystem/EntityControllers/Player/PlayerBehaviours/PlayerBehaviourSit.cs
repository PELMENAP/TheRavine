public class PlayerBehaviourSit : AState
{
    private IControllable controller;
    private Behaviour behaviourSit;
    public PlayerBehaviourSit(IControllable _controller, System.Action _delegateIdle)
    {
        controller = _controller;
        behaviourSit = _delegateIdle.Invoke;
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
        behaviourSit();
    }
}
