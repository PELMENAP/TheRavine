using TheRavine.EntityControl;
public class PlayerBehaviourSit : AState
{
    private PlayerController controller;
    private Behaviour behaviourSit;
    public PlayerBehaviourSit(PlayerController _controller, System.Action _delegateIdle)
    {
        controller = _controller;
        behaviourSit = _delegateIdle != null ? _delegateIdle.Invoke : null;
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
        behaviourSit?.Invoke();
    }
}
