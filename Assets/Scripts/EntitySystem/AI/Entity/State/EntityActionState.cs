using System.Collections.Generic;
using UnityEngine;
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
        if (!commands.TryGetValue(action, out var cmd))
        {
            Debug.Log($"[EnqueueAction] no command mapped for {action} in {GetType().Name}");
            return;
        }

        if (!cmd.CanExecute())
        {
            Debug.Log($"[EnqueueAction] {action} CanExecute=false");
            Model.Brain.GiveReward(0.25f);
            Model.Stats.Health.Value -= 3f;
            return;
        }
        Debug.Log($"[EnqueueAction] {action} added to queue");
        AddCommand(cmd);
    }

    public override void Enter() { }
    public override void Exit() { }
}