using Unity.Mathematics;

public class ApproachFoodCommand : PlannedMoveCommand
{
    public ApproachFoodCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    public override bool CanExecute() => model.CachedFoodValid;

    protected override EntityCommandStatus OnBegin()
    {
        if (!model.CachedFoodValid) return Fail();
        if (model.CachedFoodDistance <= SimulationRules.Active.EatRange) return Complete();

        float2 food = ChunkFoodIndex.CellCenter(model.CachedFoodCell);
        return BeginPlanned(MoveIntent.ApproachFood, food - model.Position2D, food, true,
            model.Tuning.DetectionRadius, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }
}
