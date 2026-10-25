using UnityEngine;
using TheRavine.EntityControl;
using System;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public partial class EntityModel
{
    private float ResolveDayPhase()
    {
        if (_dayCycle == null && !ServiceLocator.Services.TryGet(out _dayCycle)) return 0f;
        return _dayCycle.NormalizedTime.CurrentValue;
    }

    private static bool IsNightPhase(float phase)
    {
        ref readonly var r = ref SimulationRules.Frame;
        return r.NightStart > r.NightEnd
            ? phase >= r.NightStart || phase < r.NightEnd
            : phase >= r.NightStart && phase < r.NightEnd;
    }

    private bool IsAliveForTick()
        => !IsDeathPending && !IsDisposed && !Stats.IsDisposed && Stats.Hp > 0f;

    public override void UpdateEntityCycle() { }

    public void SubmitDecision(bool allowDecision)
    {
        if (!_cycleActive) return;
        Brain.EnqueueDecision(LastInput, _cycleNow, _cycleDt, allowDecision);
    }

    public bool BeginCycle()
    {
        _cycleActive = false;

        if (IsDisposed || IsDeathPending) return false;
        if (!IsActive.Value) return false;
        if (Stats.IsDisposed || Stats.Hp <= 0f) return false;

        Stats.Open();
        bool ok = RunCycle();
        Stats.Commit();
        return ok;
    }

    private bool RunCycle()
    {
        ref readonly var rules = ref SimulationRules.Frame;

        double nowD = SimulationClock.TimeD;
        float  dt   = (float)(nowD - _lastCycleTime);
        _lastCycleTime = nowD;
        if (dt <= 0f) dt = rules.CycleFallbackDt;
        if (dt > rules.MaxCycleDt) dt = rules.MaxCycleDt;
        float now = (float)nowD;

        TimeAlive += dt;
        if (TimeAlive >= MaxAge)
        {
            Mortality.NoteHarm(DeathCause.Age, 0f);
            Stats.Hp = 0f;
            return false;
        }
        UpdateHeading();
        Stress    *= math.exp(-dt / math.max(rules.StressTau, 1e-3f));
        UpdateOrnsteinUhlenbeck(dt, in rules);

        _season         = SeasonClock.Value(now);
        _winterApproach = SeasonClock.WinterApproach(now);
        _winter         = _season < -rules.WinterThreshold;

        float dayPhase = ResolveDayPhase();
        bool  night    = IsNightPhase(dayPhase);
        _night = night;

        var host = new HostState
        {
            HpFraction     = Stats.Hp / Stats.MaxHealth,
            Night          = night,
            Crowd          = _crowd,
            WeakThreshold  = rules.SkipWeakHpFraction,
            CrowdThreshold = rules.SkipCrowdCount,
            AtNest         = IsAtNest,
            HomeCrowd      = rules.HomeCompulsionCrowd,
        };
        float hpBeforeVirus = Stats.Hp;
        Virology.Step(Stats, dt, in host);
        if (Stats.Hp < hpBeforeVirus) Mortality.NoteHarm(DeathCause.Virus, Stats.Hp);
        ref var mods = ref Virology.Modifiers;
        if (mods.ForceSpeechRequested) Broadcast(Speech.Own);
        if (!IsAliveForTick()) return false;

        Vector3 pos  = Motor.Position();
        float2  self = new float2(pos.x, pos.z);
        float detect = Tuning.DetectionRadius * math.max(rules.BlindMinDetectionFraction, 1f - mods.Blind * rules.BlindStrength);

        PerceptionMarker.Begin();
        _nearest = Perception.FindNearestEntity(pos, this, detect, out _nearestDistance);
        _crowd   = CountAndRelease(Perception.FindEntitiesInRadius(pos, this, rules.CrowdRadius, Neighbors), Colony, out _kinCrowd);

        _foodValid    = false;
        _foodDistance = -1f;
        if (_foodIndex != null &&
            _foodIndex.TryFindNearestFood(pos.x, pos.z, detect, out _foodCell, out _foodDistance, Colony.ColonyId))
            _foodValid = true;

        FoodKind foodKind = FoodKind.Plant;
        bool foodInfected = false;
        float2 foodPos = float2.zero;
        if (_foodValid)
        {
            foodPos = ChunkFoodIndex.CellCenter(_foodCell);
            _foodIndex.TryGetKind(_foodCell, out foodKind, out foodInfected);
        }
        _foodKind = foodKind;

        var nest = Nest;
        IsAtNest = nest != null && nest.Contains(self);
        if (nest != null && _foodValid) nest.Mark(ColonyChannel.Food, foodPos, rules.NestFoodMark * dt);
        if (nest != null && !IsWild)
            nest.Mark(ColonyChannel.Explored, self, rules.ExploredMark * dt * (Caste == Caste.Scout ? rules.ScoutExploredMul : 1f));
        if (nest != null && !IsWild) nest.Mark(ColonyChannel.Territory, self, rules.TerritoryMark * dt);
        SampleTrail(in self, nowD, in rules);
        _nearestForeign = FindNearestForeign(pos, detect, out _nearestForeignDistance);
        _playerValid = _players != null
            && _players.TryFindNearest(self, detect, out _playerPos, out _playerDistance, out _playerTransform);
        if (!_playerValid) _playerTransform = null;
        PerceptionMarker.End();
        _nestFood = ComputeNestFood(nest, in rules);
        UpdateTerritory(nest, in self, dt, in rules);
        if (!IsWild) ComputeDirections(nest, in self, in foodPos, in rules);

        if (!_terrain.TrySample(pos.x, pos.z, out _lastTerrain))
            _lastTerrain = TerrainSample.Invalid;

        _lastDanger = ComputeDangerLevel();

        float neighborLoad = 0f;
        var nearest = CachedNearest;
        if (nearest != null) nearest.Virology.TryGetViralInputs(out neighborLoad, out _, out _);
        _kinSick = nearest != null && nearest.Colony == Colony && neighborLoad >= rules.GroomMinLoad;

        var frame = new VectorizerFrame
        {
            Health            = Stats.Hp,
            Energy            = Stats.En,
            DayPhase          = dayPhase,
            InDanger          = _lastDanger,
            TimeToBreed       = ComputeBreedReadiness(),
            NearestEntityDist = _nearestDistance,
            NearestFoodDist   = _foodValid ? _foodDistance : -1f,
            FoodDir           = _foodValid ? DirectionTo(in self, in foodPos) : float2.zero,
            EntityDir         = nearest != null ? DirectionTo(in self, Extension.Flat(nearest.Motor.Position())) : float2.zero,
            NestDir           = nest != null ? DirectionTo(in self, nest.Position) : float2.zero,
            PoiDir            = Points.TryGetNearest(in self, out float2 poi) ? DirectionTo(in self, in poi) : float2.zero,
            NestFoodDir       = nest != null && nest.HasFoodPeak ? DirectionTo(in self, nest.FoodPeak) : float2.zero,
            Stomach           = Digestion.Fill,
            WellFed           = Digestion.WellFed,
            FoodToxic         = _foodValid && foodKind == FoodKind.Toxic ? 1f : 0f,
            FoodLarge         = _foodValid && foodKind == FoodKind.Large ? 1f : 0f,
            FoodMeat          = _foodValid && foodKind == FoodKind.Meat ? 1f : 0f,
            FoodInfected      = foodInfected ? 1f : 0f,
            Carrying          = Carrying > 0f ? math.saturate(Carrying / math.max(Stats.MaxEnergy * rules.StomachCapacityFraction, 1e-3f)) : 0f,
            NestStorage       = nest != null ? math.saturate(nest.Storage / math.max(rules.NestStorageNorm, 1e-3f)) : 0f,
            LocalDanger       = nest != null ? math.saturate(nest.FieldAt(ColonyChannel.Danger, self)) : 0f,
            Alarm             = nest != null ? nest.Alarm : 0f,
            AtNest            = IsAtNest ? 1f : 0f,
            NeighborViralLoad = neighborLoad,
            Stress            = Stress,
            ColonyHunger      = nest != null ? nest.Hunger : 0f,
            FlockHeading      = _flock.MeanHeading,
            FlockCentroidDir  = _flock.CentroidDir,
            MigrationDir      = nest != null ? math.normalizesafe(nest.Migration) * math.saturate(nest.MigrationPressure) : float2.zero,
            NearestForeign    = _nearestForeign != null || _playerValid ? 1f : 0f,
            IsLeader          = IsLeader ? 1f : 0f,
            Caste             = (int)Caste,
            Speech            = Speech.Heard,
            MimickedAction    = MimickedActionIndex,
            Now               = nowD,
            PlanRewardEma     = _planRewardEma,
            Season            = _season,
            WinterApproach    = _winterApproach,
        };
        Virology.TryGetViralInputs(out frame.ViralLoad, out frame.ViralSegments, out frame.ViralNet);
        _sick = mods.Fever >= rules.SickFeverThreshold || frame.ViralLoad >= rules.SickLoadThreshold;
        if (_sick && nest != null) nest.Mark(ColonyChannel.Sickness, self, rules.SickMark * dt);
        UpdateHormones(dt, in rules);

        if (!IsWild) LastInput = Vectorizer.Vectorize(in frame, _actionTimes, in _lastTerrain);

        Speech.ConsumeOtherSpeech();
        ConsumeMimickedAction();

        bool resting = LastAction == EntityAction.Idle || LastAction == EntityAction.Rest;
        float digestMul = Tuning.DigestionMul;
        if (LastAction == EntityAction.Rest && SinceAction(EntityAction.Eat) < rules.SynergyWindow)
            digestMul *= rules.RestAfterEatDigestMul;

        Digestion.Tick(Stats, dt, resting, digestMul, mods.RegenMultiplier, in rules, out float regenCredit);

        float basal = rules.BasalEnergyDrain * Tuning.BasalDrainMul
                    * (1f - rules.WellFedBasalReduction * Digestion.WellFed)
                    * (night && !IsAtNest ? rules.NightOutsideDrainMul : 1f)
                    * (1f + mods.Fever * rules.FeverDrainMul)
                    * (_winter ? rules.WinterDrainMul : 1f)
                    * SenescenceMul(in rules)
                    * _immuneUpkeepMul;

        float hpBeforeStarve = Stats.Hp;
        Stats.Tick(dt,
            Motor.DrainEnergy(),
            mods.MetabolismMultiplier,
            basal,
            rules.StarvationThreshold, rules.StarvationDamage, rules.StarvationEnergyReturn,
            out float metabolismCredit);
        if (Stats.Hp < hpBeforeStarve) Mortality.NoteHarm(DeathCause.Starved, Stats.Hp);

        Virology.CreditDurableEffects(regenCredit, metabolismCredit, Stats.MaxEnergy);
        if (!IsAliveForTick()) return false;

        _driveIntegral += Drive() * dt;

        _runner.Tick();
        if (!IsAliveForTick()) return false;
        _plan.Tick();
        if (!IsAliveForTick()) return false;

        if (IsWild)
        {
            _threatResolved = ResolveThreatSource(in self, frame.LocalDanger, in rules, out _threatSource);
            _hasThreat   = _threatResolved;
            _cycleNow    = now;
            _cycleDt     = dt;
            _cycleActive = true;
            return true;
        }

        var goalBias = new float4(
            mods.WanderBias,
            mods.HuntBias + mods.Frenzy * rules.FrenzyHuntBias,
            mods.ForageBias,
            mods.SocialBias);
        float fleeBias = mods.FleeBias - mods.Frenzy * rules.FrenzyFleeSuppress;

        if (nest != null && nest.Alarm > 0f)
        {
            float alarm = nest.Alarm;
            if (nest.AlarmHunt) goalBias.y += alarm * rules.AlarmHuntBias;
            else
            {
                goalBias.x += alarm * rules.AlarmSurviveBias;
                fleeBias   += alarm * rules.AlarmFleeBias;
            }
        }

        var brainCtx = Brain.Context;
        for (int p = 0; p < PlanCatalog.Count; p++)
            brainCtx.CoordBias[p] = goalBias[(int)PlanCatalog.GoalOf((PlanKind)p)];
        brainCtx.CoordBias[(int)PlanKind.Flee] += fleeBias - mods.FearInvert * rules.FearInvertStrength;
        brainCtx.CoordBias[(int)PlanKind.Hunt] += mods.BiteSeek * rules.BiteSeekHuntBias;
        brainCtx.CoordBias[(int)PlanKind.Rest] += mods.HomeCompulsion * rules.HomeCompulsionRestBias;
        brainCtx.CoordBias[(int)PlanKind.Patrol] += mods.Isolate * rules.IsolateStrength;
        brainCtx.CoordBias[(int)PlanKind.Harvest] += _winterApproach * rules.PreWinterHarvestBias;
        if (night && IsAtNest && !IsHungry) brainCtx.CoordBias[(int)PlanKind.Rest] += rules.NightRestBias;
        if (_onForeignTerritory) brainCtx.CoordBias[(int)PlanKind.Hunt] -= rules.ForeignTerritoryHuntDrop;

        ref readonly var genes = ref brainCtx.CoordMLP.Params;
        bool adult = CasteFixed && !IsJuvenile && !IsWild;
        int  affinityRow = (int)Caste * PlanCatalog.Count;
        for (int p = 0; p < PlanCatalog.Count; p++)
            brainCtx.CoordBias[p] += genes.PlanBias(p) + (adult ? rules.CasteAffinityLog[affinityRow + p] : 0f);

        var colonyRef = Colony;
        if (IsLeader) colonyRef.PublishLeaderPrior(brainCtx.PlanProbs);
        else if (colonyRef != null && colonyRef.HasLeaderPrior)
        {
            float w = rules.LeaderPriorWeight, floor = rules.LeaderPriorFloor;
            for (int p = 0; p < PlanCatalog.Count; p++)
                brainCtx.CoordBias[p] += w * math.log(math.max(colonyRef.LeaderPlanProbs[p], floor));
        }

        _threatResolved = ResolveThreatSource(in self, frame.LocalDanger, in rules, out _threatSource);
        bool hasThreat = _threatResolved || frame.LocalDanger > rules.ThreatMinDanger;
        _hasThreat = hasThreat;

        brainCtx.EnergyNorm    = EffectiveEnergyNorm;
        brainCtx.PlanHints     = PlanHints;
        brainCtx.ColonyStorage = frame.NestStorage;
        brainCtx.DirectionMask = _dirMask;
        brainCtx.SetTemperatureScale(TemperatureScale(brainCtx, in rules));

        int members = Colony != null ? Colony.MemberCount : 0;
        _utility = new UtilityFeatures
        {
            Hunger         = 1f - math.saturate(brainCtx.EnergyNorm),
            Fill           = frame.Stomach,
            Storage        = frame.NestStorage,
            Threat         = hasThreat ? 1f : 0f,
            Night          = night ? 1f : 0f,
            Juveniles      = members > 0 ? Colony.JuvenileCount / (float)members : 0f,
            Worker         = adult && Caste == Caste.Worker  ? 1f : 0f,
            Soldier        = adult && Caste == Caste.Soldier ? 1f : 0f,
            Scout          = adult && Caste == Caste.Scout   ? 1f : 0f,
            Nurse          = adult && Caste == Caste.Nurse   ? 1f : 0f,
            HpDeficit      = 1f - math.saturate(Stats.Hp / Stats.MaxHealth),
            WinterApproach = _winterApproach,
        };

        float parentDistance = -1f;
        if (Parent.TryGet(out var parent)) parentDistance = math.distance(self, parent.Position2D);

        int running = _runner.CurrentAction;
        _instinctInput = new InstinctInput
        {
            HpFraction      = Stats.Hp / Stats.MaxHealth,
            EnergyFraction  = Stats.En / Stats.MaxEnergy,
            Stomach         = frame.Stomach,
            Carrying        = Carrying / math.max(CarryCapacity, 1e-3f),
            LocalDanger     = frame.LocalDanger * (1f - mods.FearInvert),
            Stress          = Stress * (1f - mods.FearInvert),
            Alarm           = frame.Alarm,
            EntityDistance  = _nearestDistance,
            FoodDistance    = _foodValid ? _foodDistance : -1f,
            Now             = now,
            ParentDistance  = parentDistance,
            Age             = TimeAlive,
            AtNest          = IsAtNest ? (byte)1 : (byte)0,
            NestFood        = _nestFood ? (byte)1 : (byte)0,
            FoodToxic       = _foodValid && foodKind == FoodKind.Toxic ? (byte)1 : (byte)0,
            Night           = night ? (byte)1 : (byte)0,
            Warned          = IsWarned ? (byte)1 : (byte)0,
            HasThreat       = hasThreat ? (byte)1 : (byte)0,
            Busy            = _runner.IsRunning ? (byte)1 : (byte)0,
            RunningAction   = running >= 0 ? (byte)running : InstinctBits.NoReflex,
            RunningInstinct = _runner.IsRunning && _runner.CurrentSource == CommandSource.Instinct ? (byte)1 : (byte)0,
            PlanActive      = _plan.IsActive ? (byte)1 : (byte)0,
            PlanKind        = (byte)_plan.Kind,
            Caste           = (byte)Caste,
            NestDistance    = nest != null ? math.distance(self, nest.Position) : -1f,
            HungerBias      = _ghrelin * rules.GhrelinHungerShift,
            PanicBias       = _oxytocin * rules.OxytocinPanicShift - _cortisol * rules.CortisolPanicShift,
            Sick            = _sick ? (byte)1 : (byte)0,
            HungryKin       = nest != null && nest.HungryAtNest > 0 ? (byte)1 : (byte)0,
            Recruited       = IsRecruited ? (byte)1 : (byte)0,
        };

        _cycleNow    = now;
        _cycleDt     = dt;
        _cycleActive = true;
        return true;
    }

    private PlanHint ComputePlanHints(bool hasThreat)
    {
        ref readonly var r = ref SimulationRules.Frame;
        PlanHint h = PlanHint.None;
        if (_foodValid) h |= PlanHint.FoodVisible;
        if (_foodValid && _foodDistance <= r.EatRange) h |= PlanHint.FoodInRange;
        if (Carrying > 0f) h |= PlanHint.Carrying;
        if (Carrying >= CarryCapacity * r.CarryFullFraction) h |= PlanHint.CarryFull;
        if (IsAtNest) h |= PlanHint.AtNest;
        if (CachedNearest != null) h |= PlanHint.EntityNear;
        if (_playerValid) h |= PlanHint.PlayerNear;
        if (ValidForeign != null || _playerValid || (IsFrenzied && CachedNearest != null)) h |= PlanHint.PreyNear;
        if (_nestFood) h |= PlanHint.NestHasFood;
        if (Stats.En >= math.max(r.AttackEnergyMin, Tuning.AttackEnergyCost)) h |= PlanHint.CanAttack;
        if (Stats.En >= Tuning.ReproduceEnergyCost && Stats.Hp >= Tuning.ReproduceHealthCost) h |= PlanHint.CanReproduce;
        if (IsSated) h |= PlanHint.Sated;
        if (Stats.Hp >= Stats.MaxHealth) h |= PlanHint.HpFull;
        if (Points.Count > 0) h |= PlanHint.HasPoi;
        if (hasThreat) h |= PlanHint.Threat;
        if (IsHungry) h |= PlanHint.Hungry;
        if (IsLeader) h |= PlanHint.IsLeader;
        if (_nearestForeign != null) h |= PlanHint.ForeignNear;
        var nest = Nest;
        if (nest != null && nest.MigrationPressure > r.MigrationMinPressure) h |= PlanHint.MigrationUrge;
        if (nest != null && nest.HasFoodPeak) h |= PlanHint.FoodPeak;
        if (nest != null && nest.Storage < r.SeekStorageLow * r.NestStorageNorm) h |= PlanHint.StorageLow;
        var colony = Colony;
        if (colony != null && colony.Pois.Count > 0) h |= PlanHint.ColonyPoi;
        if (colony != null && IsLeader && colony.TryGetQuorumSite(out _)) h |= PlanHint.QuorumReady;
        if (Caste == Caste.Worker && CasteFixed && !IsJuvenile && !IsHungry) h |= PlanHint.PreferPickUp;
        if (_kinSick) h |= PlanHint.KinSick;
        if (_night) h |= PlanHint.Night;
        return h;
    }

    public void EndCycle()
    {
        if (!_cycleActive) return;
        _cycleActive = false;

        if (IsDisposed || !IsAliveForTick())
        {
            Brain.DiscardDecision();
            return;
        }

        if (Brain.TryTakeDecision(out var decision))
        {
            Array.Copy(LastInput, _decisionInput, _decisionInput.Length);
            StartDecision(in decision);
        }

        OnUpdate.Execute(R3.Unit.Default);
    }

    private void StartDecision(in BrainDecision decision)
    {
        _plan.Interrupt();
        _runner.Interrupt();

        if (decision.HasPlan)
        {
            _plan.Begin(in decision);
            return;
        }

        BeginHomeostasis();
        if (TryStartCommand(decision.Action, in decision, CommandSource.Brain)) return;
        Brain.CompleteDecision(in decision, SimulationRules.Active.InfeasibleActionReward,
            SimulationClock.Time, EntityCommandStatus.Failed);
    }

    private float ComputeDangerLevel()
    {
        ref readonly var r = ref SimulationRules.Frame;
        float hp = Stats.Hp / Stats.MaxHealth;
        float en = Stats.En / Stats.MaxEnergy;
        float m  = math.min(hp, en);
        float d  = 0f;
        if (m < r.DangerLowFraction)  d += 0.25f;
        if (m < r.DangerMidFraction)  d += 0.25f;
        if (m < r.DangerCritFraction) d += 0.5f;
        return d;
    }

    private float ComputeBreedReadiness()
    {
        ref readonly var r = ref SimulationRules.Frame;
        float hp = Stats.Hp, en = Stats.En;
        float ec = Tuning.ReproduceEnergyCost, hc = Tuning.ReproduceHealthCost;
        float b = 0f;
        if (en > ec && hp > hc) b += 0.5f;
        if (en > ec + Stats.MaxEnergy * r.BreedMarginFraction &&
            hp > hc + Stats.MaxHealth * r.BreedMarginFraction) b += 0.5f;
        return b;
    }
}
