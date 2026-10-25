using Unity.Mathematics;

public class GoToPointCommand : PlannedMoveCommand
{
    public GoToPointCommand(EntityModel model) : base(model) { }

    public override bool CanExecute()
        => model.IsRecruited || model.Points.Count > 0 || (model.Colony != null && model.Colony.Pois.Count > 0);

    protected override EntityCommandStatus OnBegin()
    {
        float2 self = model.Position2D;
        float2 target;
        bool own;

        if (Source == CommandSource.Instinct && model.IsRecruited)
        {
            target = model.RecruitTarget;
            own    = true;
        }
        else if (decision.Direction == (byte)DirectionKind.OwnPoi)
            own = model.Points.TryPickBest(in self, out target);
        else if (decision.Direction == (byte)DirectionKind.ColonyPoi && model.Colony != null)
            own = model.Colony.Pois.TryPickBest(self, out target, out _);
        else
        {
            own = model.Points.TryPickBest(in self, out target);
            var colony = model.Colony;
            if (colony != null && colony.Pois.TryPickBest(self, out float2 shared, out _)
                && (!own || math.distancesq(shared, self) < math.distancesq(target, self)))
            {
                target = shared;
                own = true;
            }
        }
        if (!own) return Fail();

        return BeginPlanned(MoveIntent.GoToPOI, target - self, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        float2 self = model.Position2D;
        model.Points.Visit(in self);
        if (Source == CommandSource.Instinct) model.ClearRecruit();
        if (!model.CachedFoodValid) model.Points.MarkNegative(in self, SimulationRules.Frame.NegativePoiEmpty);
        return Complete();
    }
}
