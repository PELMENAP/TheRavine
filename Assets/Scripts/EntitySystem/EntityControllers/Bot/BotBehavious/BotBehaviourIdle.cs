public class BotBehaviourIdle : AState
{
    public Behaviour behaviourIdle;
    public override void Enter()
    {

    }

    public override void Exit()
    {
        CancelCurrentCommand();
    }

    public override async void Update()
    {
        behaviourIdle?.Invoke();
        await ProcessCommandsAsync();
    }
}
