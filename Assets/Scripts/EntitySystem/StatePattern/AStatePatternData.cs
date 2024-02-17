using System.Collections.Generic;

public interface IStatePatternComponent : IComponent
{
    AState behaviourCurrent { get; set; }
    Dictionary<System.Type, AState> behavioursMap { get; set; }
    void SetBehaviour(AState newBehaviour);
    AState GetBehaviour<T>() where T : AState => behavioursMap[typeof(T)];
}

public class StatePatternComponent : IStatePatternComponent
{
    public AState behaviourCurrent { get; set; }
    public Dictionary<System.Type, AState> behavioursMap { get; set; }
    public StatePatternComponent()
    {
        behavioursMap = new Dictionary<System.Type, AState>();
    }
    public void SetBehaviour(AState newBehaviour)
    {
        if (behaviourCurrent != null)
            behaviourCurrent.Exit();
        behaviourCurrent = newBehaviour;
        behaviourCurrent.Enter();
    }
    public void AddBehaviour(System.Type type, AState behaviour)
    {
        behavioursMap[type] = behaviour;
    }
    public AState GetBehaviour<T>() where T : AState => behavioursMap[typeof(T)];
}
