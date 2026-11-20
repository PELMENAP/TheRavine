public partial class EntityModel
{
    public void RegisterFitnessEvent(FitnessEvent evt, float amount = 0f)
    {
        _fitness.Register(evt, amount);
        if (_descriptors == null) return;
        if (evt == FitnessEvent.FoodEaten) _descriptors.NoteFood(ManagerIndex);
        else if (evt == FitnessEvent.DamageDealt) _descriptors.NoteAttack(ManagerIndex);
    }

    public float GetFitness() => _fitness.Evaluate(TimeAlive);

    public void CaptureFinalFitness() => _fitness.Capture(TimeAlive);
}
