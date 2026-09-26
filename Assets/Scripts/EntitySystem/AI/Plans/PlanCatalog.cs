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
    Count,
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
    };

    public static readonly string[] Names = { "Graze", "Harvest", "Patrol", "Hunt", "Rest", "Flee", "Court", "Migrate" };

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
                return Has(h, PlanHint.FoodVisible) ? Bit(EntityAction.ApproachFood) : 0;

            case PlanKind.Harvest:
                if (Has(h, PlanHint.Carrying) && (Has(h, PlanHint.CarryFull) || !Has(h, PlanHint.FoodVisible)))
                    return Has(h, PlanHint.AtNest) ? Bit(EntityAction.StoreFood) : Bit(EntityAction.ReturnNest);
                if (Has(h, PlanHint.FoodInRange)) return Bit(EntityAction.PickUp);
                return Has(h, PlanHint.FoodVisible) ? Bit(EntityAction.ApproachFood) : 0;

            case PlanKind.Patrol:
                return Bit(EntityAction.Wander) | (Has(h, PlanHint.HasPoi) ? Bit(EntityAction.GoToPoint) : 0);

            case PlanKind.Hunt:
                return Has(h, PlanHint.PreyNear) && Has(h, PlanHint.CanAttack)
                    ? Bit(EntityAction.Attack) | Bit(EntityAction.Threaten)
                    : 0;

            case PlanKind.Rest:
                if (Has(h, PlanHint.Hungry) && (Has(h, PlanHint.FoodVisible) || Has(h, PlanHint.NestHasFood))) return 0;
                return Bit(EntityAction.Rest) | (Has(h, PlanHint.AtNest) ? 0 : Bit(EntityAction.ReturnNest));

            case PlanKind.Flee:
                return Has(h, PlanHint.Threat) ? Bit(EntityAction.Flee) : 0;

            case PlanKind.Migrate:
                return Has(h, PlanHint.MigrationUrge) ? Bit(EntityAction.Wander) : 0;

            case PlanKind.Court:
                return Bit(EntityAction.Speech)
                     | (Has(h, PlanHint.EntityNear) ? Bit(EntityAction.ShareFood) | Bit(EntityAction.Mimic) : 0)
                     | (Has(h, PlanHint.CanReproduce) ? Bit(EntityAction.Reproduce) : 0);
        }
        return 0;
    }

    public static int Next(ref PlanProgress p, PlanHint h, EntityCommandStatus status, int lastAction)
    {
        var r = SimulationRules.Active;
        bool ok = status == EntityCommandStatus.Completed;
        if (!ok && ++p.Failures > r.PlanMaxFailures) return Fail;
        if (ok && lastAction == (int)EntityAction.Eat) p.Ate = true;

        switch (p.Kind)
        {
            case PlanKind.Graze:
                p.Achieved = p.Ate;
                if (Has(h, PlanHint.Sated)) return Complete;
                if (Has(h, PlanHint.FoodInRange) || Has(h, PlanHint.NestHasFood)) return (int)EntityAction.Eat;
                if (Has(h, PlanHint.FoodVisible)) return (int)EntityAction.ApproachFood;
                return p.Ate ? Complete : Fail;

            case PlanKind.Harvest:
                if (lastAction == (int)EntityAction.StoreFood)
                {
                    p.Achieved = ok;
                    return ok ? Complete : Fail;
                }
                if (p.Phase == 0)
                {
                    bool carrying = Has(h, PlanHint.Carrying);
                    if (Has(h, PlanHint.CarryFull) || (carrying && !Has(h, PlanHint.FoodVisible))) p.Phase = 1;
                    else if (Has(h, PlanHint.FoodInRange)) return (int)EntityAction.PickUp;
                    else if (Has(h, PlanHint.FoodVisible)) return (int)EntityAction.ApproachFood;
                    else return Fail;
                }
                if (p.Phase == 1)
                {
                    if (!Has(h, PlanHint.Carrying)) return Fail;
                    if (!Has(h, PlanHint.AtNest)) return (int)EntityAction.ReturnNest;
                    p.Phase = 2;
                    return (int)EntityAction.StoreFood;
                }
                return Fail;

            case PlanKind.Patrol:
                if (ok && lastAction != (int)EntityAction.RememberPoint)
                {
                    p.Steps++;
                    if (Has(h, PlanHint.FoodVisible))
                    {
                        p.Achieved = true;
                        return (int)EntityAction.RememberPoint;
                    }
                }
                if (p.Steps >= r.PatrolLegs) return Complete;
                return p.Variant == (int)EntityAction.GoToPoint && Has(h, PlanHint.HasPoi)
                    ? (int)EntityAction.GoToPoint
                    : (int)EntityAction.Wander;

            case PlanKind.Hunt:
                if (lastAction == (int)EntityAction.Attack && ok) p.Steps++;
                p.Achieved = p.Steps > 0;
                if (!Has(h, PlanHint.PreyNear)) return p.Steps > 0 ? Complete : Fail;
                if (p.Steps >= r.HuntMaxStrikes || !Has(h, PlanHint.CanAttack)) return p.Steps > 0 ? Complete : Fail;
                return (int)EntityAction.Attack;

            case PlanKind.Rest:
                if (lastAction == (int)EntityAction.Rest && !ok && Has(h, PlanHint.Hungry)) return Fail;
                if (lastAction == (int)EntityAction.Rest && ok) p.Steps++;
                if (p.Steps >= r.RestMaxSteps || (p.Steps > 0 && Has(h, PlanHint.HpFull))) return Complete;
                return (int)EntityAction.Rest;

            case PlanKind.Flee:
                p.Steps++;
                if (!Has(h, PlanHint.Threat) || p.Steps >= r.FleeMaxLegs) return Complete;
                return (int)EntityAction.Flee;

            case PlanKind.Migrate:
                if (lastAction == (int)EntityAction.MoveNest)
                {
                    p.Achieved = ok;
                    return ok ? Complete : Fail;
                }
                if (ok && lastAction == (int)EntityAction.Wander) p.Steps++;
                if (p.Steps < r.MigrateLegs) return (int)EntityAction.Wander;
                return Has(h, PlanHint.IsLeader) ? (int)EntityAction.MoveNest : Complete;

            case PlanKind.Court:
                if (ok && lastAction == (int)EntityAction.Reproduce) p.Achieved = true;
                if (p.Phase == 0 && lastAction != (int)EntityAction.Reproduce && Has(h, PlanHint.CanReproduce))
                {
                    p.Phase = 1;
                    return (int)EntityAction.Reproduce;
                }
                return ok ? Complete : Fail;
        }
        return Fail;
    }
}
