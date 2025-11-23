public class BotBehaviourSit : AState
{
    public Behaviour behaviourSit;
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
