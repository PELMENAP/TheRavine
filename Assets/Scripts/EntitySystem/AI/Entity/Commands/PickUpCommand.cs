

using TheRavine.EntityControl.Virology;

public class PickUpCommand : EntityCommand
{
    public PickUpCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.Carrying < model.CarryCapacity * SimulationRules.Active.CarryFullFraction;

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        var index = model.FoodIndex;
        if (index == null || !model.CachedFoodValid || model.CachedFoodDistance > r.EatRange) return Fail();

        long cell = model.CachedFoodCell;
        if (!index.TryGetKind(cell, out FoodKind kind, out _) || kind == FoodKind.Large) return Fail();

        model.InvalidateCachedFood();
        if (!index.TryClaim(cell, model, SimulationClock.TimeD, out var claim) || claim.Energy <= 0f) return Fail();
        if (claim.Infected && index.TryTakePayload(cell, out var payload)) ViralPayloadPool.Release(ref payload);
        model.NoteFoodSite(ChunkFoodIndex.CellCenter(cell));

        model.Carry(claim.Energy);
        return Complete();
    }
}
