using Unity.Mathematics;

public class WanderCommand : PlannedMoveCommand
{
    public WanderCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        float2 desired;
        float  radius = model.Tuning.WanderRadius;
        _reached = false;

        var nest = model.Nest;
        if (decision.Plan == PlanKind.Migrate && nest != null && nest.MigrationPressure > 1e-4f)
        {
            desired = math.normalizesafe(nest.Migration);
            radius  = math.min(r.MigrateLegRadius, r.PlannerRadiusMax);
        }
        else if (decision.HasDestination)
        {
            float2 self = model.Position2D;
            float2 to   = decision.Destination - self;
            float  dist = math.length(to);
            _reached = dist <= r.PlanArriveRadius;
            if (_reached) return Complete();
            return BeginPlanned(MoveIntent.Wander, to, decision.Destination, true,
                math.min(dist, r.PlannerRadiusMax), model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
        }
        else if (decision.HasDirection && model.TryResolveDirection(decision.Direction, decision.DirOffset, out desired, out float dirRadius))
            radius = math.max(dirRadius, r.LevyMinStep);
        else if (decision.HasHeading) desired = decision.Heading;
        else if (!TryFieldGradient(r, radius, out desired))
        {
            float2 course = model.Heading;
            if (math.lengthsq(course) < 1e-6f)
            {
                float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
                math.sincos(a, out float sa, out float ca);
                course = new float2(ca, sa);
            }
            math.sincos(model.OuAngle, out float so, out float co);
            desired = new float2(course.x * co - course.y * so, course.x * so + course.y * co);
            radius  = math.min(model.LevyStep(in SimulationRules.Frame), r.PlannerRadiusMax);
        }

        float2 dir = TerrainSteering.Blend(in desired, in model.LastTerrain);
        return BeginPlanned(MoveIntent.Wander, dir, default, false,
            radius, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    private bool TryFieldGradient(SimulationRules r, float radius, out float2 dir)
    {
        dir = float2.zero;
        var nest = model.Nest;
        if (nest == null) return false;

        float phase  = RavineRandom.RangeFloat(0f, 2f * math.PI);
        float spread = nest.SampleDirection(model.Position2D, radius, r.WanderGradientSamples, phase, 1f, 1f, out dir);
        return spread > r.WanderGradientMin;
    }

    private bool _reached;

    protected override bool PauseAfterArrival => !decision.HasDestination || _reached;

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        if (decision.HasDestination)
            _reached = math.distance(model.Position2D, decision.Destination) <= SimulationRules.Frame.PlanArriveRadius;
        if (decision.Plan != PlanKind.Migrate || model.Caste != Caste.Scout) return Complete();
        var colony = model.Colony;
        if (colony == null || colony.IsWild) return Complete();

        ref readonly var r = ref SimulationRules.Frame;
        float2 here = model.Position2D;
        if (!colony.Nest.Contains(here) && colony.Nest.FieldAt(ColonyChannel.Danger, here) < r.QuorumMaxDanger)
            colony.VoteNestSite(here, model.EntityId);
        return Complete();
    }
}
