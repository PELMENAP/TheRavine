public class BrainComponent : IComponent
{
    private SharedHierarchicalBrain _brain;
    private int _batchSlot = -1;

    public EntityBrainContext Context { get; }
    public BrainDecision ActiveDecision { get; private set; }
    public void CompleteTerminal(float penalty) => _brain.CompleteTerminal(Context, penalty);

    public BrainComponent(SharedHierarchicalBrain brain, EntityBrainContext ctx)
    {
        _brain = brain;
        Context = ctx;
    }

    public void ReplaceBrain(SharedHierarchicalBrain brain)
    {
        _brain = brain;
        _batchSlot = -1;
    }

    public void BeginBatch() => _brain.BeginDecisionBatch();
    public void RunBatch()   => _brain.RunDecisions();

    public bool EnqueueDecision(float[] input, float simTime, float dt)
    {
        _batchSlot = _brain.EnqueueDecision(Context, input, simTime, dt);
        return _batchSlot >= 0;
    }

    public bool TryTakeDecision(out BrainDecision decision)
    {
        int slot = _batchSlot;
        _batchSlot = -1;

        if (slot < 0 || !_brain.TryGetDecision(slot, out decision))
        {
            decision = default;
            return false;
        }

        ActiveDecision = decision;
        return true;
    }

    public void DiscardDecision() => _batchSlot = -1;

    public bool IsBusy(float simTime) => Context.ExecWindow.IsRunning(simTime);

    public void GiveReward(float reward, in BrainDecision decision) =>
        _brain.GiveReward(reward, in decision, Context);

    public void CompleteDecision(in BrainDecision decision, float reward, float simTime, EntityCommandStatus status) =>
        _brain.CompleteDecision(in decision, reward, Context, simTime, status);

    public SharedHierarchicalBrain.Goal CurrentGoal => Context.CurrentGoal;

    public void Dispose() => Context.Dispose();
}