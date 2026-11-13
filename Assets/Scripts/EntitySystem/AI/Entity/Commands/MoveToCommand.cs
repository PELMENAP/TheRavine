using Unity.Mathematics;

public class MoveToCommand : PlannedMoveCommand
{
    private enum Mode : byte { Explore, Poi, Food, Nest, Escape, Kin }

    private Mode   _mode;
    private bool   _reached;
    private bool   _fromBoard;
    private long   _cell;
    private float2 _threat;
    private int    _leg;
    private int    _legs;
    private float  _legLength;

    public MoveToCommand(EntityModel model) : base(model) { }

    protected override bool PauseAfterArrival => _mode switch
    {
        Mode.Explore => !decision.HasDestination || _reached,
        Mode.Poi     => true,
        _            => false,
    };

    private static Mode ModeOf(in BrainDecision d)
    {
        if (d.HasDestination) return Mode.Explore;
        switch (d.Direction)
        {
            case (byte)DirectionKind.OwnPoi:
            case (byte)DirectionKind.ColonyPoi: return Mode.Poi;
            case (byte)DirectionKind.Food:      return Mode.Food;
            case (byte)DirectionKind.Nest:      return Mode.Nest;
            case (byte)DirectionKind.Escape:    return Mode.Escape;
            case (byte)DirectionKind.Kin:       return Mode.Kin;
            default:                            return Mode.Explore;
        }
    }

    public override bool CanExecute(in BrainDecision d)
    {
        switch (ModeOf(in d))
        {
            case Mode.Poi:  return model.IsRecruited || model.Points.Count > 0 || (model.Colony != null && model.Colony.Pois.Count > 0);
            case Mode.Food: return model.CachedFoodValid;
            case Mode.Nest: return model.HasHome;
            case Mode.Kin:  return model.TryGetKinTarget(out _);
            default:        return true;
        }
    }

    protected override EntityCommandStatus OnBegin()
    {
        _mode = ModeOf(in decision);
        switch (_mode)
        {
            case Mode.Poi:    return BeginPoi();
            case Mode.Food:   return BeginFood();
            case Mode.Nest:   return BeginNest();
            case Mode.Escape: return BeginEscape();
            case Mode.Kin:    return BeginKin();
            default:          return BeginExplore();
        }
    }

    protected override EntityCommandStatus OnArrived(in MoveResult move)
    {
        switch (_mode)
        {
            case Mode.Explore: return ArrivedExplore();
            case Mode.Poi:     return ArrivedPoi();
            case Mode.Food:    return ArrivedFood(in move);
            case Mode.Escape:  return ArrivedEscape();
            default:           return Complete();
        }
    }

    private EntityCommandStatus BeginExplore()
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

    private EntityCommandStatus ArrivedExplore()
    {
        if (decision.HasDestination)
            _reached = math.distance(model.Position2D, decision.Destination) <= SimulationRules.Frame.PlanArriveRadius;
        if (decision.Plan != PlanKind.Migrate || !model.CasteFixed || model.IsJuvenile) return Complete();
        var colony = model.Colony;
        if (colony == null || colony.IsWild) return Complete();

        ref readonly var r = ref SimulationRules.Frame;
        float2 here = model.Position2D;
        if (!colony.Nest.Contains(here) && colony.Nest.FieldAt(ColonyChannel.Danger, here) < r.QuorumMaxDanger)
            colony.VoteNestSite(here, model.EntityId, model.Caste == Caste.Scout ? r.ScoutVoteWeight : 1f);
        return Complete();
    }

    private EntityCommandStatus BeginPoi()
    {
        float2 self = model.Position2D;
        float2 target;
        bool own;
        _fromBoard = false;

        if (Source == CommandSource.Instinct && model.IsRecruited)
        {
            target     = model.RecruitTarget;
            own        = true;
            _fromBoard = true;
        }
        else if (decision.Direction == (byte)DirectionKind.OwnPoi)
            own = model.Points.TryPickBest(in self, out target);
        else if (model.Colony != null)
        {
            own        = model.Colony.Pois.TryPickBest(self, out target, out _);
            _fromBoard = true;
        }
        else
        {
            target = default;
            own    = false;
        }
        if (!own) return Fail();

        return BeginPlanned(MoveIntent.GoToPOI, target - self, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    private EntityCommandStatus ArrivedPoi()
    {
        float2 self = model.Position2D;
        model.Points.Visit(in self);
        if (Source == CommandSource.Instinct) model.ClearRecruit();
        if (model.CachedFoodValid) return Complete();

        model.Points.MarkNegative(in self, SimulationRules.Frame.NegativePoiEmpty);
        model.Points.ReportEmpty(in self);
        var colony = model.Colony;
        if (_fromBoard && colony != null && !colony.IsWild) colony.Pois.ReportEmpty(self);
        return Complete();
    }

    private EntityCommandStatus BeginFood()
    {
        if (!model.CachedFoodValid) return Fail();
        if (model.CachedFoodDistance <= SimulationRules.Active.EatRange) return Complete();

        _cell = model.CachedFoodCell;
        float2 food = ChunkFoodIndex.CellCenter(_cell);
        return BeginPlanned(MoveIntent.ApproachFood, food - model.Position2D, food, true,
            model.Tuning.DetectionRadius, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    private EntityCommandStatus ArrivedFood(in MoveResult move)
    {
        if (!move.Blocked) return Complete();

        var r = SimulationRules.Active;
        float2 food = ChunkFoodIndex.CellCenter(_cell);
        if (math.distance(model.Position2D, food) <= r.EatRange) return Complete();

        model.FoodIndex?.MarkUnreachable(_cell, SimulationClock.TimeD + r.UnreachableFoodSeconds);
        return Fail();
    }

    private EntityCommandStatus BeginNest()
    {
        if (!model.HasHome) return Fail();
        if (model.IsAtNest) return Complete();

        float2 home = model.HomePosition;
        return BeginPlanned(MoveIntent.ReturnNest, home - model.Position2D, home, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }

    private EntityCommandStatus BeginEscape()
    {
        var r = SimulationRules.Active;
        float2 self  = model.Position2D;
        float  total = model.Tuning.DetectionRadius * r.FleeDistanceMul;

        if (!model.TryGetThreatSource(out _threat) && !TryDangerSource(r, self, total, out _threat)) return Complete();

        _legs      = math.max(1, r.FleeZigzagLegs);
        _leg       = 0;
        _legLength = _legs > 1 ? math.max(total - r.FleeSprintDistance, 0f) / (_legs - 1) : total;

        float speed = model.Tuning.RunSpeed * (model.IsWarned ? r.WarnedFleeSpeedMul : 1f);
        float first = _legs > 1 ? math.min(r.FleeSprintDistance, total) : total;
        return BeginPlanned(MoveIntent.Flee, self - _threat, default, false,
            first, speed, model.Tuning.EnergyCostRunning, _threat, true, 1f, 0f);
    }

    private EntityCommandStatus ArrivedEscape()
    {
        if (++_leg >= _legs || _legLength <= SimulationRules.Frame.FleeMinLegLength) return Complete();

        var r = SimulationRules.Active;
        float2 self = model.Position2D;
        float  side = (_leg & 1) == 0 ? 1f : -1f;
        bool   threatened = model.TryGetThreatSource(out _) || (model.InstinctLatches & InstinctBits.Panic) != 0;
        float  speed = threatened ? model.Tuning.RunSpeed * (model.IsWarned ? r.WarnedFleeSpeedMul : 1f) : model.Tuning.MoveSpeed;
        float  cost  = threatened ? model.Tuning.EnergyCostRunning : model.Tuning.EnergyCostMoving;
        return BeginPlanned(MoveIntent.Flee, self - _threat, default, false,
            _legLength, speed, cost, _threat, true,
            side, r.FleeZigzagCurvature);
    }

    private bool TryDangerSource(SimulationRules r, float2 self, float radius, out float2 source)
    {
        source = default;
        var nest = model.Nest;
        if (nest == null) return false;

        float spread = nest.SampleDirection(self, radius, r.WanderGradientSamples, 0f, 0f, -1f, out float2 toDanger);
        if (spread <= r.WanderGradientMin) return false;
        source = self + toDanger * radius;
        return true;
    }

    private EntityCommandStatus BeginKin()
    {
        if (!model.TryGetKinTarget(out float2 target)) return Fail();

        float2 self = model.Position2D;
        if (math.distance(self, target) <= SimulationRules.Active.TetherRadius) return Complete();

        return BeginPlanned(MoveIntent.GoToPOI, target - self, target, true,
            SimulationRules.Active.PlannerRadiusMax, model.Tuning.MoveSpeed, model.Tuning.EnergyCostMoving);
    }
}
