public class IdleCommand : EntityCommand
{
    public IdleCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        model.Motor.Stop();
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(decision.EndTime) ? Complete() : EntityCommandStatus.Running;
}
