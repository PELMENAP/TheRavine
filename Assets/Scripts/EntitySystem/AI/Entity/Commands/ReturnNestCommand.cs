using Unity.Mathematics;

public class ReturnNestCommand : PlannedMoveCommand
{
    public ReturnNestCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    public override bool CanExecute() => model.Nest != null;

    protected override EntityCommandStatus OnBegin()
    {
        var nest = model.Nest;
        if (nest == null) return Fail();
        if (model.IsAtNest) return Complete();

        float2 home = nest.Position;
        return BeginPlanned(MoveIntent.ReturnNest, home - model.Position2D, home, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }
}
