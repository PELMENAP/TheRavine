using UnityEngine;  

public class BrainComponent : IComponent
{
    private readonly SharedHierarchicalBrain _brain;
    public EntityBrainContext Context { get; }

    public BrainComponent(SharedHierarchicalBrain brain, EntityBrainContext ctx)
    {
        _brain = brain;
        Context = ctx;
    }

    public int Predict(float[] input) => _brain.Predict(input, Context);
    public void GiveReward(float reward) 
    {
        _brain.GiveReward(reward, Context);
    }
    public SharedHierarchicalBrain.Goal CurrentGoal => Context.CurrentGoal;

    public void Dispose() { }
}