using Unity.Mathematics;

public class MoveNestCommand : EntityCommand
{
    public MoveNestCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => TryGetSite(out _);

    private bool TryGetSite(out float2 site)
    {
        site = default;
        var colony = model.Colony;
        if (model.IsWild || !model.CasteFixed || model.IsJuvenile || colony == null ||
            !colony.TryGetQuorumSite(out site) || colony.Nest.Contains(site)) return false;

        float radius = colony.Nest.Radius;
        return math.distancesq(model.Position2D, site) <= radius * radius;
    }

    protected override EntityCommandStatus OnBegin()
    {
        if (!TryGetSite(out float2 site) || !model.IsDryNestSite(site)) return Fail();
        model.Colony.Relocate(site, model.FoodIndex);
        return Complete();
    }
}
