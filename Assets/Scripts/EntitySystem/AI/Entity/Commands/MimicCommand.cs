public class MimicCommand : EntityCommand
{
    public MimicCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var other = model.CachedNearest;
        if (other == null) return Fail();

        model.SetMimickedAction(other.LastActionIndex);
        return Complete();
    }
}
