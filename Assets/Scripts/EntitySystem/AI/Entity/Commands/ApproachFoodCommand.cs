using Unity.Mathematics;

public class ApproachFoodCommand : PlannedMoveCommand
{
    public ApproachFoodCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    private long _cell;

    public override bool CanExecute() => model.CachedFoodValid;

    protected override EntityCommandStatus OnBegin()
    {
        if (!model.CachedFoodValid) return Fail();
        if (model.CachedFoodDistance <= SimulationRules.Active.EatRange) return Complete();

        _cell = model.CachedFoodCell;
        float2 food = ChunkFoodIndex.CellCenter(_cell);
        return BeginPlanned(MoveIntent.ApproachFood, food - model.Position2D, food, true,
            model.Tuning.DetectionRadius, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        if (!move.Blocked) return Complete();

        var r = SimulationRules.Active;
        float2 food = ChunkFoodIndex.CellCenter(_cell);
        if (math.distance(model.Position2D, food) <= r.EatRange) return Complete();

        model.FoodIndex?.MarkUnreachable(_cell, SimulationClock.TimeD + r.UnreachableFoodSeconds);
        return Fail();
    }
}
