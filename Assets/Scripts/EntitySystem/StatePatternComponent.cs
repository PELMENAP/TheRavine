using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;


public interface IStatePatternComponent : IComponent
{
    AState behaviourCurrent { get; set; }
    UniTask SetBehaviourAsync(AState newBehaviour);
    void AddBehaviour(Type type, AState behaviour);
    AState GetBehaviour<T>() where T : AState;
}

public class StatePatternComponent : IStatePatternComponent
{
    public AState behaviourCurrent { get; set; }
    private Dictionary<Type, AState> behavioursMap { get; set; }

    public StatePatternComponent()
    {
        behavioursMap = new Dictionary<Type, AState>();
    }

    public async UniTask SetBehaviourAsync(AState newBehaviour)
    {
        if (behaviourCurrent != null)
        {
            behaviourCurrent.CancelCurrentCommand();
            behaviourCurrent.ClearCommands();
            behaviourCurrent.Exit();
        }
        behaviourCurrent = newBehaviour;
        behaviourCurrent.Enter();
        behaviourCurrent.ResetCancellation();
    }

    public void AddBehaviour(Type type, AState behaviour)
    {
        behavioursMap[type] = behaviour;
    }

    public AState GetBehaviour<T>() where T : AState => behavioursMap[typeof(T)];

    public void Dispose()
    {
        foreach (var state in behavioursMap.Values)
        {
            state.Dispose();
        }
        behavioursMap.Clear();
    }
}
