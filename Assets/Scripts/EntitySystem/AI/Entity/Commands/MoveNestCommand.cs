using Unity.Mathematics;

public class MoveNestCommand : EntityCommand
{
    public MoveNestCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.IsLeader && model.Colony != null && model.Colony.TryGetQuorumSite(out float2 site)
           && !model.Colony.Nest.Contains(site);

    protected override EntityCommandStatus OnBegin()
    {
        var colony = model.Colony;
        if (colony == null || !model.IsLeader || !colony.TryGetQuorumSite(out float2 site) || colony.Nest.Contains(site))
            return Fail();
        colony.Relocate(site, model.FoodIndex);
        return Complete();
    }
}
