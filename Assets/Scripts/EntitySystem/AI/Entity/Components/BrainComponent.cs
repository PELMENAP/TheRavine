public class BrainComponent : IComponent
{
    private SharedHierarchicalBrain _brain;
    public EntityBrainContext Context { get; }
    public BrainDecision ActiveDecision { get; private set; }
    public void CompleteTerminal(float penalty) => _brain.CompleteTerminal(Context, penalty);

    public BrainComponent(SharedHierarchicalBrain brain, EntityBrainContext ctx)
    {
        _brain = brain;
        Context = ctx;
    }

    public void ReplaceBrain(SharedHierarchicalBrain brain) => _brain = brain;

    public bool TryDecide(float[] input, float simTime, float dt, out BrainDecision decision)
    {
        bool made = _brain.TryDecide(input, Context, simTime, dt, out decision);
        if (made) ActiveDecision = decision;
        return made;
    }

    public bool IsBusy(float simTime) => Context.ExecWindow.IsRunning(simTime);

    public void GiveReward(float reward, int decisionId) =>
        _brain.GiveReward(reward, decisionId, Context);

    public void CompleteDecision(int decisionId, float reward, float simTime, EntityCommandStatus status) =>
        _brain.CompleteDecision(decisionId, reward, Context, simTime, status);

    public SharedHierarchicalBrain.Goal CurrentGoal => Context.CurrentGoal;

    public void Dispose() { }
}