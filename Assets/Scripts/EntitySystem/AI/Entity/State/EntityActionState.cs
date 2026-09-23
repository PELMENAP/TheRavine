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

    public void EnqueueAction(EntityAction action, in BrainDecision decision)
    {
        if (!commands.TryGetValue(action, out var cmd))
        {
            Model.Brain.CompleteDecision(in decision, -0.2f,
                SimulationClock.Time, EntityCommandStatus.Failed);
            return;
        }

        if (!cmd.CanExecute())
        {
            float penalty = SimulationRules.Active.InfeasibleActionHealthPenalty;
            if (penalty > 0f) Model.Stats.Health.Value -= penalty;
            Model.Brain.CompleteDecision(in decision, -0.15f,
                SimulationClock.Time, EntityCommandStatus.Failed);
            return;
        }

        AddCommand(cmd);
    }

    public override void Enter() { }
    public override void Exit() { }
}