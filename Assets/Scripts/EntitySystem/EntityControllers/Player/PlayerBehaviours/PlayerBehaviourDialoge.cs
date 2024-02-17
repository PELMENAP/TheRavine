public class PlayerBehaviourDialoge : AState
{
    public Behaviour behaviourDialoge;
    public override void Enter()
    {

    }

    public override void Exit()
    {
        CancelCurrentCommand();
    }

    public override void Update()
    {
        behaviourDialoge();
    }
}
