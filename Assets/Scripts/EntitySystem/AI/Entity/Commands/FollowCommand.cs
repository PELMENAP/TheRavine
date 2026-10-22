using Unity.Mathematics;

public class FollowCommand : PlannedMoveCommand
{
    public FollowCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => false;

    public override bool CanExecute() => model.Parent.TryGet(out _);

    protected override EntityCommandStatus OnBegin()
    {
        if (!model.Parent.TryGet(out var parent)) return Fail();

        float2 self   = model.Position2D;
        float2 target = parent.Position2D;
        if (math.distance(self, target) <= SimulationRules.Active.TetherRadius) return Complete();

        return BeginPlanned(MoveIntent.GoToPOI, target - self, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.RunSpeed, model.Tuning.EnergyCostRunning);
    }
}
