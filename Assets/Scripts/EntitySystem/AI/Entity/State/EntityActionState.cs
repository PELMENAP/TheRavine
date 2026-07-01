using System.Collections.Generic;

public abstract class EntityActionState : AState
{
    protected readonly EntityModel Model;
    private readonly Dictionary<EntityAction, ICommand> commands;

    protected EntityActionState(EntityModel model, Dictionary<EntityAction, ICommand> _commands)
    {
        Model = model;
        commands = _commands;
    }

    public void EnqueueAction(EntityAction action)
    {
        if (commands.TryGetValue(action, out var cmd))
            AddCommand(cmd);
    }

    public override void Enter() { }
    public override void Exit() { }
}

public class SurviveState : EntityActionState
{
    public SurviveState(EntityModel model) : base(model, BuildCommands(model)) { }

    private static Dictionary<EntityAction, ICommand> BuildCommands(EntityModel m) => new()
    {
        [EntityAction.Idle] = new IdleCommand(m),
        [EntityAction.Wander] = new WanderCommand(m),
        [EntityAction.Flee] = new FleeCommand(m),
        [EntityAction.Eat] = new EatCommand(m),
        [EntityAction.Rest] = new RestCommand(m),
    };
}