using TheRavine.EntityControl.Virology;

public class TakeCommand : EntityCommand
{
    public TakeCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.Carrying < model.CarryCapacity * model.CarryFullFraction;

    protected override EntityCommandStatus OnBegin()
    {
        if (decision.Direction == (byte)DirectionKind.Object) return Fail(ColonyStats.TakeFail.Kind);

        var r = SimulationRules.Active;
        var index = model.FoodIndex;
        if (index == null || !model.CachedFoodValid || model.CachedFoodDistance > r.EatRange) return Fail(ColonyStats.TakeFail.NoFood);

        long cell = model.CachedFoodCell;
        if (!index.TryGetKind(cell, out FoodKind kind, out _) || kind == FoodKind.Large) return Fail(ColonyStats.TakeFail.Kind);

        model.InvalidateCachedFood();
        if (!index.TryClaim(cell, model, SimulationClock.TimeD, out var claim)) return Fail(ColonyStats.TakeFail.Claim, 0f);
        if (claim.Energy <= 0f) return Fail(ColonyStats.TakeFail.Claim);
        if (claim.Infected && index.TryTakePayload(cell, out var payload)) ViralPayloadPool.Release(ref payload);
        model.NoteFoodSite(ChunkFoodIndex.CellCenter(cell));

        model.Carry(claim.Energy);
        return Complete();
    }

    private EntityCommandStatus Fail(ColonyStats.TakeFail reason)
    {
        model.Colony?.Stats.RecordTakeFail(reason);
        return Fail();
    }

    private EntityCommandStatus Fail(ColonyStats.TakeFail reason, float reward)
    {
        model.Colony?.Stats.RecordTakeFail(reason);
        return Fail(reward);
    }
}
