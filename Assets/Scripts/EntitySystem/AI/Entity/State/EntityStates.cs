using System.Collections.Generic;

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

public class HuntState : EntityActionState
{
    public HuntState(EntityModel model) : base(model, BuildCommands(model)) { }

    private static Dictionary<EntityAction, ICommand> BuildCommands(EntityModel m) => new()
    {
        [EntityAction.Idle] = new IdleCommand(m),
        [EntityAction.Wander] = new WanderCommand(m),
        [EntityAction.Attack] = new AttackCommand(m),
        [EntityAction.Flee] = new FleeCommand(m),
        [EntityAction.Threaten] = new ThreatenCommand(m),
    };
}

public class ForageState : EntityActionState
{
    public ForageState(EntityModel model) : base(model, BuildCommands(model)) { }

    private static Dictionary<EntityAction, ICommand> BuildCommands(EntityModel m) => new()
    {
        [EntityAction.Wander] = new WanderCommand(m),
        [EntityAction.RememberPoint] = new RememberPointCommand(m),
        [EntityAction.GoToPoint] = new GoToPointCommand(m),
        [EntityAction.Eat] = new EatCommand(m),
    };
}

public class SocialState : EntityActionState
{
    public SocialState(EntityModel model) : base(model, BuildCommands(model)) { }

    private static Dictionary<EntityAction, ICommand> BuildCommands(EntityModel m) => new()
    {
        [EntityAction.Idle] = new IdleCommand(m),
        [EntityAction.Wander] = new WanderCommand(m),
        [EntityAction.Reproduce] = new ReproduceCommand(m),
        [EntityAction.Speech] = new SpeechCommand(m),
        [EntityAction.Mimic] = new MimicCommand(m),
        [EntityAction.ShareFood] = new ShareFoodCommand(m),
    };
}