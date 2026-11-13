using System;

public enum PlanKind : byte
{
    Graze   = 0,
    Harvest = 1,
    Patrol  = 2,
    Hunt    = 3,
    Rest    = 4,
    Flee    = 5,
    Court   = 6,
    Migrate = 7,
    Seek    = 8,
    Count,
}

public enum DirectionKind : byte
{
    Food      = 0,
    NestFood  = 1,
    OwnPoi    = 2,
    ColonyPoi = 3,
    Frontier  = 4,
    Migration = 5,
    Flock     = 6,
    Levy      = 7,
    Nest      = 8,
    Escape    = 9,
    Kin       = 10,
    Danger    = 11,
    Object    = 12,
    Task      = 13,
    Count,
    None      = byte.MaxValue,
}

[Flags]
public enum PlanHint : uint
{
    None         = 0,
    FoodVisible  = 1 << 0,
    FoodInRange  = 1 << 1,
    Carrying     = 1 << 2,
    CarryFull    = 1 << 3,
    AtNest       = 1 << 4,
    EntityNear   = 1 << 5,
    CanAttack    = 1 << 6,
    CanReproduce = 1 << 7,
    Sated        = 1 << 8,
    HpFull       = 1 << 9,
    HasPoi       = 1 << 10,
    Threat       = 1 << 11,
    Hungry       = 1 << 12,
    MigrationUrge = 1 << 13,
    IsLeader     = 1 << 14,
    ForeignNear  = 1 << 15,
    PreyNear     = 1 << 16,
    PlayerNear   = 1 << 17,
    NestHasFood  = 1 << 18,
    StorageLow   = 1 << 19,
    ColonyPoi    = 1 << 20,
    FoodPeak     = 1 << 21,
    PreferPickUp = 1 << 22,
    QuorumReady  = 1 << 23,
    KinSick      = 1 << 24,
    AtDestination = 1 << 25,
    Night        = 1 << 26,
    HasNest      = 1 << 27,
    HasHome      = 1 << 28,
    RestBlocked  = 1 << 29,
    TargetWeak   = 1 << 30,
}

public struct PlanProgress
{
    public PlanKind Kind;
    public int  Variant;
    public int  Phase;
    public int  Steps;
    public int  Failures;
    public bool Ate;
    public bool Achieved;
    public int  Legs;
    public int  Used;
    public byte Direction;
    public byte LegDirection;
    public byte VariantDirection;
    public int  Hops;
    public int  MaxFailures;
    public int  MaxStrikes;
    public bool HasDestination;
    public bool NeedsDestination;
    public Unity.Mathematics.float2 Destination;
}

public static class PlanCatalog
{
    public const int Count = (int)PlanKind.Count;
    public const int Complete = -1;
    public const int Fail     = -2;

    private static readonly SharedHierarchicalBrain.Goal[] Goals =
    {
        SharedHierarchicalBrain.Goal.Forage,
        SharedHierarchicalBrain.Goal.Forage,
        SharedHierarchicalBrain.Goal.Survive,
        SharedHierarchicalBrain.Goal.Hunt,
        SharedHierarchicalBrain.Goal.Survive,
        SharedHierarchicalBrain.Goal.Survive,
        SharedHierarchicalBrain.Goal.Social,
        SharedHierarchicalBrain.Goal.Survive,
        SharedHierarchicalBrain.Goal.Forage,
    };

    public static readonly string[] Names = { "Graze", "Harvest", "Patrol", "Hunt", "Rest", "Flee", "Court", "Migrate", "Seek" };

    public static void Init(ref PlanProgress p)
    {
        p.Direction        = (byte)DirectionKind.None;
        p.LegDirection     = (byte)DirectionKind.None;
        p.VariantDirection = (byte)DirectionKind.None;
        if (p.Kind != PlanKind.Seek) return;
        ref readonly var r = ref SimulationRules.Frame;
        int lo = Math.Max(1, r.SeekMinLegs);
        p.Legs = RavineRandom.RangeInt(lo, Math.Max(lo, r.SeekMaxLegs) + 1);
    }

    public static bool IsTravelPlan(PlanKind plan) => plan == PlanKind.Patrol || plan == PlanKind.Seek;

    private static bool CompleteHop(ref PlanProgress p, PlanHint h, int maxHops)
    {
        p.Hops++;
        if (p.HasDestination && !Has(h, PlanHint.AtDestination) && p.Hops < maxHops) return false;
        p.Hops = 0;
        p.NeedsDestination = true;
        return true;
    }

    private static byte NextSeekDirection(ref PlanProgress p, PlanHint h)
    {
        if ((p.Used & 1) == 0 && Has(h, PlanHint.ColonyPoi)) { p.Used |= 1; return (byte)DirectionKind.ColonyPoi; }
        if ((p.Used & 2) == 0 && Has(h, PlanHint.HasPoi))    { p.Used |= 2; return (byte)DirectionKind.OwnPoi; }
        if ((p.Used & 4) == 0 && Has(h, PlanHint.FoodPeak))  { p.Used |= 4; return (byte)DirectionKind.NestFood; }
        p.Used |= 7;
        return (p.Steps & 1) == 0 ? (byte)DirectionKind.Frontier : (byte)DirectionKind.Levy;
    }

    private static ushort Dir(DirectionKind kind) => (ushort)(1 << (int)kind);

    private const ushort AllDirections = ushort.MaxValue;

    private const ushort ExploreDirections =
        (ushort)((1 << (int)DirectionKind.NestFood) | (1 << (int)DirectionKind.OwnPoi) | (1 << (int)DirectionKind.ColonyPoi)
               | (1 << (int)DirectionKind.Frontier) | (1 << (int)DirectionKind.Migration) | (1 << (int)DirectionKind.Flock)
               | (1 << (int)DirectionKind.Levy) | (1 << (int)DirectionKind.Danger) | (1 << (int)DirectionKind.Object)
               | (1 << (int)DirectionKind.Task));

    public static bool IsPoiDirection(byte direction)
        => direction == (byte)DirectionKind.OwnPoi || direction == (byte)DirectionKind.ColonyPoi;

    public static bool IsExploratory(byte direction)
        => !IsPoiDirection(direction) && direction != (byte)DirectionKind.Food && direction != (byte)DirectionKind.Nest
        && direction != (byte)DirectionKind.Escape && direction != (byte)DirectionKind.Kin;

    public static bool IsAnchored(byte direction)
        => direction <= (byte)DirectionKind.ColonyPoi
        || (direction >= (byte)DirectionKind.Nest && direction < (byte)DirectionKind.Count && direction != (byte)DirectionKind.Escape);

    public static ushort DirectionMask(PlanKind plan, PlanHint h)
    {
        switch (plan)
        {
            case PlanKind.Graze:
                return Dir(DirectionKind.Food);

            case PlanKind.Harvest:
                return Has(h, PlanHint.Carrying) && (Has(h, PlanHint.CarryFull) || !Has(h, PlanHint.FoodVisible))
                    ? Dir(DirectionKind.Nest)
                    : Dir(DirectionKind.Food);

            case PlanKind.Patrol:
                return ExploreDirections;

            case PlanKind.Rest:
                return Dir(DirectionKind.Nest);

            case PlanKind.Flee:
                return Dir(DirectionKind.Escape);

            case PlanKind.Migrate:
                return (ushort)(Dir(DirectionKind.Migration) | Dir(DirectionKind.Levy));

            case PlanKind.Seek:
                return Has(h, PlanHint.FoodVisible) && !Has(h, PlanHint.FoodInRange)
                    ? Dir(DirectionKind.Food)
                    : ExploreDirections;
        }
        return AllDirections;
    }

    private static int Step(ref PlanProgress p, EntityAction action, DirectionKind direction = DirectionKind.None)
    {
        p.Direction = (byte)direction;
        return (int)action;
    }

    public static SharedHierarchicalBrain.Goal GoalOf(PlanKind plan) => Goals[(int)plan];

    private static bool Has(PlanHint h, PlanHint flag) => (h & flag) != 0;
    private static int Bit(EntityAction a) => 1 << (int)a;

    public static bool IsFeasible(PlanKind plan, PlanHint h, int casteMask) => (EntryMask(plan, h) & casteMask) != 0;

    public static int EntryMask(PlanKind plan, PlanHint h)
    {
        switch (plan)
        {
            case PlanKind.Graze:
                if (Has(h, PlanHint.Sated)) return 0;
                if (Has(h, PlanHint.FoodInRange) || Has(h, PlanHint.NestHasFood)) return Bit(EntityAction.Eat);
                return Has(h, PlanHint.FoodVisible) ? Bit(EntityAction.MoveTo) : 0;

            case PlanKind.Harvest:
                if (!Has(h, PlanHint.HasNest)) return 0;
                if (Has(h, PlanHint.Carrying) && (Has(h, PlanHint.CarryFull) || !Has(h, PlanHint.FoodVisible)))
                    return Has(h, PlanHint.AtNest) ? Bit(EntityAction.Put) : Bit(EntityAction.MoveTo);
                if (Has(h, PlanHint.FoodInRange)) return Bit(EntityAction.Take);
                return Has(h, PlanHint.FoodVisible) ? Bit(EntityAction.MoveTo) : 0;

            case PlanKind.Patrol:
                return Bit(EntityAction.MoveTo);

            case PlanKind.Hunt:
                return Has(h, PlanHint.PreyNear) && Has(h, PlanHint.CanAttack)
                    ? Bit(EntityAction.Attack) | Bit(EntityAction.Signal)
                    : 0;

            case PlanKind.Rest:
                if (Has(h, PlanHint.RestBlocked) && (Has(h, PlanHint.FoodVisible) || Has(h, PlanHint.NestHasFood))) return 0;
                return Bit(EntityAction.Rest) | (Has(h, PlanHint.AtNest) || !Has(h, PlanHint.HasHome) ? 0 : Bit(EntityAction.MoveTo));

            case PlanKind.Flee:
                return Has(h, PlanHint.Threat) ? Bit(EntityAction.MoveTo) : 0;

            case PlanKind.Migrate:
                return Has(h, PlanHint.MigrationUrge) || Has(h, PlanHint.QuorumReady) ? Bit(EntityAction.MoveTo) : 0;

            case PlanKind.Seek:
                if (!Has(h, PlanHint.Hungry) && !Has(h, PlanHint.StorageLow)) return 0;
                if (Has(h, PlanHint.FoodInRange))
                    return Has(h, PlanHint.PreferPickUp) && !Has(h, PlanHint.CarryFull) ? Bit(EntityAction.Take)
                         : Has(h, PlanHint.Sated) ? Bit(EntityAction.MoveTo) : Bit(EntityAction.Eat);
                return Bit(EntityAction.MoveTo);

            case PlanKind.Court:
                return Bit(EntityAction.Signal)
                     | (Has(h, PlanHint.EntityNear) || Has(h, PlanHint.KinSick) ? Bit(EntityAction.Give) : 0)
                     | (Has(h, PlanHint.CanReproduce) ? Bit(EntityAction.Reproduce) : 0);
        }
        return 0;
    }

    public static int Next(ref PlanProgress p, PlanHint h, EntityCommandStatus status, int lastAction)
    {
        var r = SimulationRules.Active;
        bool ok = status == EntityCommandStatus.Completed;
        if (!ok && ++p.Failures > p.MaxFailures) return Fail;
        if (ok && lastAction == (int)EntityAction.Eat) p.Ate = true;

        switch (p.Kind)
        {
            case PlanKind.Graze:
                p.Achieved = p.Ate;
                if (Has(h, PlanHint.Sated)) return Complete;
                if (Has(h, PlanHint.FoodInRange) || Has(h, PlanHint.NestHasFood)) return Step(ref p, EntityAction.Eat);
                if (Has(h, PlanHint.FoodVisible)) return Step(ref p, EntityAction.MoveTo, DirectionKind.Food);
                return p.Ate ? Complete : Fail;

            case PlanKind.Harvest:
                if (lastAction == (int)EntityAction.Put)
                {
                    p.Achieved = ok;
                    return ok ? Complete : Fail;
                }
                if (p.Phase == 0)
                {
                    bool carrying = Has(h, PlanHint.Carrying);
                    if (Has(h, PlanHint.CarryFull) || (carrying && !Has(h, PlanHint.FoodVisible))) p.Phase = 1;
                    else if (Has(h, PlanHint.FoodInRange)) return Step(ref p, EntityAction.Take, DirectionKind.Food);
                    else if (Has(h, PlanHint.FoodVisible)) return Step(ref p, EntityAction.MoveTo, DirectionKind.Food);
                    else return Fail;
                }
                if (p.Phase == 1)
                {
                    if (!Has(h, PlanHint.Carrying)) return Fail;
                    if (!Has(h, PlanHint.AtNest)) return Step(ref p, EntityAction.MoveTo, DirectionKind.Nest);
                    p.Phase = 2;
                    return Step(ref p, EntityAction.Put);
                }
                return Fail;

            case PlanKind.Patrol:
            {
                bool leg    = lastAction == (int)EntityAction.MoveTo;
                bool poiLeg = leg && IsPoiDirection(p.Direction);
                if (ok && leg && (poiLeg || CompleteHop(ref p, h, r.PlanMaxHops)))
                {
                    p.Steps++;
                    if (Has(h, PlanHint.FoodVisible))
                    {
                        p.Achieved = true;
                        return Step(ref p, EntityAction.Signal);
                    }
                }
                if (p.Steps >= r.PatrolLegs) return Complete;
                bool poiVariant = p.Variant == (int)EntityAction.MoveTo && IsPoiDirection(p.VariantDirection);
                return poiVariant && Has(h, PlanHint.HasPoi)
                    ? Step(ref p, EntityAction.MoveTo, (DirectionKind)p.VariantDirection)
                    : Step(ref p, EntityAction.MoveTo);
            }

            case PlanKind.Hunt:
                if (lastAction == (int)EntityAction.Attack && ok) p.Steps++;
                p.Achieved = p.Steps > 0;
                if (!Has(h, PlanHint.PreyNear)) return p.Steps > 0 ? Complete : Fail;
                bool finishing = Has(h, PlanHint.TargetWeak) && Has(h, PlanHint.CanAttack);
                if ((p.Steps >= p.MaxStrikes && !finishing) || !Has(h, PlanHint.CanAttack)) return p.Steps > 0 ? Complete : Fail;
                return Step(ref p, EntityAction.Attack);

            case PlanKind.Rest:
                if (lastAction == (int)EntityAction.Rest && !ok && Has(h, PlanHint.RestBlocked)) return Fail;
                if (lastAction == (int)EntityAction.Rest && ok) p.Steps++;
                if (Has(h, PlanHint.Night) && Has(h, PlanHint.AtNest) && !Has(h, PlanHint.RestBlocked)) return Step(ref p, EntityAction.Rest);
                if (p.Steps >= r.RestMaxSteps || (p.Steps > 0 && Has(h, PlanHint.HpFull))) return Complete;
                return Step(ref p, EntityAction.Rest);

            case PlanKind.Flee:
                p.Steps++;
                if (!Has(h, PlanHint.Threat) || p.Steps >= r.FleeMaxLegs) return Complete;
                return Step(ref p, EntityAction.MoveTo, DirectionKind.Escape);

            case PlanKind.Migrate:
                if (lastAction == (int)EntityAction.MoveNest)
                {
                    p.Achieved = ok;
                    return ok ? Complete : Fail;
                }
                if (Has(h, PlanHint.QuorumReady)) return Step(ref p, EntityAction.MoveNest);
                if (ok && lastAction == (int)EntityAction.MoveTo) p.Steps++;
                if (p.Steps < r.MigrateLegs) return Step(ref p, EntityAction.MoveTo, DirectionKind.Migration);
                return Complete;

            case PlanKind.Seek:
                if (ok && lastAction == (int)EntityAction.Take) p.Achieved = true;
                if (p.Ate) p.Achieved = true;
                if (Has(h, PlanHint.FoodInRange))
                {
                    if (Has(h, PlanHint.PreferPickUp) && !Has(h, PlanHint.CarryFull)) return Step(ref p, EntityAction.Take, DirectionKind.Food);
                    if (!Has(h, PlanHint.Sated)) return Step(ref p, EntityAction.Eat);
                }
                bool wantsFood = !Has(h, PlanHint.Sated) || (Has(h, PlanHint.PreferPickUp) && !Has(h, PlanHint.CarryFull));
                if (Has(h, PlanHint.FoodVisible) && wantsFood) return Step(ref p, EntityAction.MoveTo, DirectionKind.Food);
                if (p.Achieved) return Complete;
                bool seekLeg = lastAction == (int)EntityAction.MoveTo && p.Direction != (byte)DirectionKind.Food;
                if (ok && seekLeg && CompleteHop(ref p, h, r.PlanMaxHops))
                {
                    p.Steps++;
                    if (p.Steps >= p.Legs) return Fail;
                    p.LegDirection = NextSeekDirection(ref p, h);
                }
                return Step(ref p, EntityAction.MoveTo);

            case PlanKind.Court:
                if (ok && lastAction == (int)EntityAction.Reproduce) p.Achieved = true;
                if (p.Phase == 0 && lastAction != (int)EntityAction.Reproduce && Has(h, PlanHint.CanReproduce))
                {
                    p.Phase = 1;
                    return Step(ref p, EntityAction.Reproduce);
                }
                return ok ? Complete : Fail;
        }
        return Fail;
    }
}
