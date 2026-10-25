public partial class EntityModel
{
    public void RegisterFitnessEvent(FitnessEvent evt, float amount = 0f) => _fitness.Register(evt, amount);

    public float GetFitness() => _fitness.Evaluate(TimeAlive);

    public void CaptureFinalFitness() => _fitness.Capture(TimeAlive);
}
