using System;

[Flags]
public enum ActionFlags : byte
{
    None        = 0,
    IsMove      = 1 << 0,
    NeedsTarget = 1 << 1,
    EnergyGated = 1 << 2,
    InstinctOnly = 1 << 3,
}

public readonly struct ActionInfo
{
    public readonly ActionFlags Flags;
    public readonly byte GoalMask;
    public readonly byte CasteMask;

    public ActionInfo(ActionFlags flags, byte goalMask, byte casteMask)
    {
        Flags     = flags;
        GoalMask  = goalMask;
        CasteMask = casteMask;
    }

    public bool Has(ActionFlags flag) => (Flags & flag) != 0;
}

public static class ActionCatalog
{
    public const int Count = (int)EntityAction.Follow + 1;

    private const byte Survive = 1 << (int)SharedHierarchicalBrain.Goal.Survive;
    private const byte Hunt    = 1 << (int)SharedHierarchicalBrain.Goal.Hunt;
    private const byte Forage  = 1 << (int)SharedHierarchicalBrain.Goal.Forage;
    private const byte Social  = 1 << (int)SharedHierarchicalBrain.Goal.Social;

    private const byte AllCastes = byte.MaxValue;

    private static readonly ActionInfo[] Table = BuildTable();

    private static ActionInfo[] BuildTable()
    {
        var t = new ActionInfo[Count];
        Set(t, EntityAction.Idle,          ActionFlags.None,                              Survive | Hunt | Social);
        Set(t, EntityAction.Wander,        ActionFlags.IsMove,                            Survive);
        Set(t, EntityAction.RememberPoint, ActionFlags.None,                              Forage);
        Set(t, EntityAction.GoToPoint,     ActionFlags.IsMove | ActionFlags.NeedsTarget,  Survive | Forage);
        Set(t, EntityAction.Attack,        ActionFlags.IsMove | ActionFlags.NeedsTarget | ActionFlags.EnergyGated, Hunt);
        Set(t, EntityAction.Flee,          ActionFlags.IsMove,                            Survive | Hunt);
        Set(t, EntityAction.Eat,           ActionFlags.None,                              Survive | Forage);
        Set(t, EntityAction.Reproduce,     ActionFlags.EnergyGated,                       Social);
        Set(t, EntityAction.Speech,        ActionFlags.None,                              Social);
        Set(t, EntityAction.Mimic,         ActionFlags.NeedsTarget,                       Social);
        Set(t, EntityAction.Rest,          ActionFlags.None,                              Survive);
        Set(t, EntityAction.Threaten,      ActionFlags.NeedsTarget,                       Hunt);
        Set(t, EntityAction.ShareFood,     ActionFlags.NeedsTarget,                       Social);
        Set(t, EntityAction.ApproachFood,  ActionFlags.IsMove | ActionFlags.NeedsTarget,  Survive | Hunt | Forage);
        Set(t, EntityAction.ReturnNest,    ActionFlags.IsMove,                            Survive | Forage | Social);
        Set(t, EntityAction.PickUp,        ActionFlags.NeedsTarget,                       Forage);
        Set(t, EntityAction.StoreFood,     ActionFlags.None,                              Forage);
        Set(t, EntityAction.Follow,        ActionFlags.IsMove | ActionFlags.NeedsTarget | ActionFlags.InstinctOnly, 0);
        return t;
    }

    private static void Set(ActionInfo[] t, EntityAction a, ActionFlags flags, int goalMask)
        => t[(int)a] = new ActionInfo(flags, (byte)goalMask, AllCastes);

    public static ref readonly ActionInfo Get(int action) => ref Table[action];
    public static ref readonly ActionInfo Get(EntityAction action) => ref Table[(int)action];

    public static bool Has(int action, ActionFlags flag) => (uint)action < Count && Table[action].Has(flag);

    public static bool AllowedForCaste(int action, Caste caste)
        => (uint)action < Count && (Table[action].CasteMask & (1 << (int)caste)) != 0;

    public static int[][] BuildSubsets(int goalCount)
    {
        var subsets = new int[goalCount][];
        for (int g = 0; g < goalCount; g++)
        {
            int n = 0;
            for (int a = 0; a < Count; a++)
                if (InSubset(a, g)) n++;

            var subset = new int[n];
            n = 0;
            for (int a = 0; a < Count; a++)
                if (InSubset(a, g)) subset[n++] = a;
            subsets[g] = subset;
        }
        return subsets;
    }

    private static bool InSubset(int action, int goal)
    {
        ref readonly var info = ref Table[action];
        return (info.GoalMask & (1 << goal)) != 0 && !info.Has(ActionFlags.InstinctOnly);
    }
}
