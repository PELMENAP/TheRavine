public class PlayerBehaviourDialogue : AState
{
    public Behaviour behaviourDialogue;
    public override void Enter()
    {

    }

    public override void Exit()
    {
        CancelCurrentCommand();
    }

    public override void Update()
    {
        behaviourDialogue();
    }
}
