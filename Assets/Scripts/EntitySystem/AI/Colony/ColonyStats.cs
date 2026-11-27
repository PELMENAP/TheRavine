using Unity.Mathematics;

public sealed class ColonyStats
{
    public enum AttackTarget : byte { Own = 0, Foreign = 1, Player = 2, Count = 3 }
    public enum EatFail : byte { NoFood = 0, Claim = 1, Empty = 2, LargeNoPartner = 3, Count = 4 }
    public enum TakeFail : byte { NoFood = 0, Kind = 1, Claim = 2, Count = 3 }

    public int   Born;
    public int   Died;
    public float LifespanSum;
    public float LifespanEma;
    public bool  HasLifespan;

    public readonly int[] DeathsByCause   = new int[(int)DeathCause.Lightning + 1];
    public readonly int[] CommandsBySource = new int[(int)CommandSource.Count];
    public readonly int[] PlansStarted    = new int[PlanCatalog.Count];
    public readonly int[] PlansCompleted  = new int[PlanCatalog.Count];
    public readonly int[] Attacks         = new int[(int)AttackTarget.Count];
    public readonly int[] ActionsStarted  = new int[ActionCatalog.Count];
    public const int PhaseBins = 50;
    public readonly int[] GoalsStarted    = new int[SharedHierarchicalBrain.GoalCount];
    public readonly int[] ActionBigrams   = new int[ActionCatalog.Count * ActionCatalog.Count];
    public readonly int[] GoalPhase       = new int[SharedHierarchicalBrain.GoalCount * PhaseBins];
    public readonly int[] ActionsStartedBySource = new int[ActionCatalog.Count * (int)CommandSource.Count];
    public readonly int[] ActionsCompleted   = new int[ActionCatalog.Count];
    public readonly int[] ActionsFailed      = new int[ActionCatalog.Count];
    public readonly int[] ActionsInterrupted = new int[ActionCatalog.Count];
    public readonly int[] PlansByCaste    = new int[(int)Caste.Count * PlanCatalog.Count];
    public float FarShare;
    public int   GuestDeaths;
    public float GuestLifespanSum;
    public int   GuestFailed;
    public int   Reseeds;
    public int   ForeignHarmTicks;
    public int   WarmthDrawTicks;
    public int   Relocations;
    public int   AttackFailNoTarget;
    public int   AttackFailTargetLost;
    public int   AttackFailLowEnergy;
    public int   AttackFailLurkTimeout;
    public readonly int[] EatFails  = new int[(int)EatFail.Count];
    public readonly int[] TakeFails = new int[(int)TakeFail.Count];

    public float StorageEaten;
    public float StorageStored;
    public float MeanFill;
    public float RestAtNestShare;
    public float StorageShare;
    public float DietMean;
    public float GhrelinMean;
    public float HungryShare;

    private float  _deathRateEma;
    private double _lastDeathTime = double.NaN;
    public float DeathRatePerMinute => _deathRateEma;

    public float MeanLifespan => Died > 0 ? LifespanSum / Died : 0f;

    public enum BirthFailure : byte { Capacity, ParentReserve, ChildReserve, Incompatibility, Spawn, Inviable, Count }
    public readonly int[] BirthFailures = new int[(int)BirthFailure.Count];

    public enum Interaction : byte
    {
        Fed, Groomed, GiftEnergy, FeedNoTarget, FeedRecipientSated, FeedEmpty, GroomNoPatient,
        Spoken, Marked, Threatened, Delivered, DeliveredEnergy, DeliverySeconds,
        RareActionStarted, RareActionCompleted, RareActionReward,
        ObjectsTaken, ObjectsPlaced, ObjectDeliveryEnergy, Count
    }

    public readonly double[] Interactions = new double[(int)Interaction.Count];

    public void RecordInteraction(Interaction kind, double amount = 1d) => Interactions[(int)kind] += amount;

    public enum StressSource : byte { Threat, Hunger, Cold, Lonely, Task, Contagion, Count }

    public readonly double[] StressAdded = new double[(int)StressSource.Count];
    public double StressSeconds;
    public double StressHighSeconds;

    public enum ReliefKind : byte { Comfort, Feed, Rest, Nest, Count }

    public readonly double[] ReliefSum = new double[(int)ReliefKind.Count];
    public int ComfortStarted;
    public int ComfortCompleted;
    public int GiveTransfers;
    public int GiveReciprocal;

    public void RecordRelief(ReliefKind kind, float amount) => ReliefSum[(int)kind] += amount;
    public void RecordComfortStarted() => ComfortStarted++;
    public void RecordComfortCompleted() => ComfortCompleted++;

    public void RecordStress(StressSource source, float amount) => StressAdded[(int)source] += amount;

    public void RecordStressTime(float dt, bool high)
    {
        StressSeconds += dt;
        if (high) StressHighSeconds += dt;
    }

    public void RecordBirth() => Born++;

    public void RecordDeath(float lifespan, DeathCause cause)
    {
        Died++;
        LifespanSum += lifespan;
        DeathsByCause[(int)cause]++;

        float a = SimulationRules.Active.StatsEmaAlpha;
        LifespanEma = HasLifespan ? math.lerp(LifespanEma, lifespan, a) : lifespan;
        HasLifespan = true;

        double now = SimulationClock.TimeD;
        if (!double.IsNaN(_lastDeathTime))
        {
            float gap  = (float)math.max(now - _lastDeathTime, 1e-3);
            float rate = 60f / gap;
            _deathRateEma = math.lerp(_deathRateEma, rate, a);
        }
        _lastDeathTime = now;
    }

    public void RecordAttack(AttackTarget target) => Attacks[(int)target]++;
    public void RecordForeignHarm() => ForeignHarmTicks++;
    public void RecordWarmthDraw() => WarmthDrawTicks++;
    public void RecordAction(int action) { if ((uint)action < (uint)ActionsStarted.Length) ActionsStarted[action]++; }
    public void RecordGuestDeath(float lifespan)
    {
        GuestDeaths++;
        GuestLifespanSum += lifespan;
    }

    public void RecordGuestFailed() => GuestFailed++;
    public void RecordHabit(int previous, int action, int goal, float dayPhase)
    {
        if ((uint)goal >= (uint)GoalsStarted.Length) return;
        GoalsStarted[goal]++;
        int bin = math.clamp((int)(math.frac(dayPhase) * PhaseBins), 0, PhaseBins - 1);
        GoalPhase[goal * PhaseBins + bin]++;
        if ((uint)previous < (uint)ActionCatalog.Count && (uint)action < (uint)ActionCatalog.Count)
            ActionBigrams[previous * ActionCatalog.Count + action]++;
    }
    public void RecordActionSource(int action, CommandSource source) { if ((uint)action < (uint)ActionsStarted.Length) ActionsStartedBySource[action * (int)CommandSource.Count + (int)source]++; }
    public void RecordCompleted(int action) { if ((uint)action < (uint)ActionsCompleted.Length) ActionsCompleted[action]++; }
    public void RecordFailed(int action) { if ((uint)action < (uint)ActionsFailed.Length) ActionsFailed[action]++; }
    public void RecordInterrupted(int action) { if ((uint)action < (uint)ActionsInterrupted.Length) ActionsInterrupted[action]++; }
    public void RecordRelocation() => Relocations++;
    public void RecordAttackFailNoTarget()    => AttackFailNoTarget++;
    public void RecordAttackFailTargetLost()  => AttackFailTargetLost++;
    public void RecordAttackFailLowEnergy()   => AttackFailLowEnergy++;
    public void RecordAttackFailLurkTimeout() => AttackFailLurkTimeout++;
    public void RecordEatFail(EatFail reason)   => EatFails[(int)reason]++;
    public void RecordTakeFail(TakeFail reason) => TakeFails[(int)reason]++;

    public void RecordCastePlan(Caste caste, PlanKind plan)
    {
        if (caste >= Caste.Count || plan >= PlanKind.Count) return;
        PlansByCaste[(int)caste * PlanCatalog.Count + (int)plan]++;
    }

    public PlanKind TopPlan(Caste caste)
    {
        int row = (int)caste * PlanCatalog.Count, best = -1, bestCount = 0;
        for (int p = 0; p < PlanCatalog.Count; p++)
            if (PlansByCaste[row + p] > bestCount) { bestCount = PlansByCaste[row + p]; best = p; }
        return best < 0 ? PlanKind.Count : (PlanKind)best;
    }
    public void RecordStorageEaten(float amount)  => StorageEaten  += amount;
    public void RecordStorageStored(float amount) => StorageStored += amount;

    public void SampleMembers(float meanFill, float restAtNestShare, float storageShare)
    {
        MeanFill        = meanFill;
        RestAtNestShare = restAtNestShare;
        StorageShare    = storageShare;
    }

    public void SampleDiagnostics(float diet, float ghrelin, float hungry)
    {
        DietMean    = diet;
        GhrelinMean = ghrelin;
        HungryShare = hungry;
    }

    public float PlanShare(PlanKind plan)
    {
        int total = 0;
        for (int i = 0; i < PlansStarted.Length; i++) total += PlansStarted[i];
        return total > 0 ? PlansStarted[(int)plan] / (float)total : 0f;
    }

    public void RecordCommand(CommandSource source) => CommandsBySource[(int)source]++;

    public void RecordPlan(PlanKind plan, bool completed)
    {
        if (plan >= PlanKind.Count) return;
        PlansStarted[(int)plan]++;
        if (completed) PlansCompleted[(int)plan]++;
    }

    public float SourceFraction(CommandSource source)
    {
        int total = 0;
        for (int i = 0; i < CommandsBySource.Length; i++) total += CommandsBySource[i];
        return total > 0 ? CommandsBySource[(int)source] / (float)total : 0f;
    }
}
