public class GroomCommand : EntityCommand
{
    private double _holdEnd;

    public GroomCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var patient = model.FindSickestKin(r.EatRange * r.KinSearchRangeMul, r.GroomMinLoad);
        if (patient == null) return Fail();

        model.Motor.Stop();
        patient.Virology?.Groom(r.GroomAmount);
        model.Infection?.TryTransmitContact(patient, model, r.GroomInfectChance);
        _holdEnd = HoldUntil(model.Tuning.IdleTime);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete() : EntityCommandStatus.Running;
}
