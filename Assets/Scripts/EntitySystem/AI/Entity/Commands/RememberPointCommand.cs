using Unity.Mathematics;

public class RememberPointCommand : EntityCommand
{
    public RememberPointCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        float2 pos = model.Position2D;
        model.Points.TryRemember(in pos, SimulationRules.Active.RememberPointMinSpacing);
        if (model.Caste == Caste.Scout)
            model.Colony?.Pois.Offer(pos, model.CachedFoodValid ? SimulationRules.Active.EatEnergyFood * model.CasteMods.PoiWeight : 0f, 0f);
        return Complete();
    }
}
