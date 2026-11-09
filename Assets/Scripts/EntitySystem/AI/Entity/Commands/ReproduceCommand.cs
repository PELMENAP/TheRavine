public class ReproduceCommand : EntityCommand
{
    private double _holdEnd;

    public ReproduceCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() =>
        model.Stats.En >= model.Tuning.ReproduceEnergyCost &&
        model.Stats.Hp >= model.Tuning.ReproduceHealthCost;

    protected override EntityCommandStatus OnBegin()
    {
        if (!model.RequestReproduce()) return Fail();

        model.Stats.En -= model.Tuning.ReproduceEnergyCost;
        model.Ledger.Flows.Reproduce += model.Tuning.ReproduceEnergyCost;
        model.Stats.Hp -= model.Tuning.ReproduceHealthCost;
        model.RegisterFitnessEvent(EntityModel.FitnessEvent.Reproduced);

        _holdEnd = HoldUntil(model.Tuning.IdleTime);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete(SimulationRules.Active.EventReproduceReward) : EntityCommandStatus.Running;
}
