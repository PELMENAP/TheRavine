using UnityEngine;
using TheRavine.EntityControl;
using System;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public class EntityModel : AEntity, IFoodReceiver
{
    public enum FitnessEvent { FoodEaten, Reproduced, DamageDealt }

    private const int ActionCount = ActionCatalog.Count;

    public StatsComponent Stats { get; private set; }
    public PerceptionComponent Perception { get; private set; }
    public BrainComponent Brain { get; private set; }
    public SpeechComponent Speech { get; private set; }
    public PointsOfInterestComponent Points { get; private set; }
    public VirologyComponent Virology { get; private set; }
    public DigestionComponent Digestion { get; private set; }

    public IEntityDialogHost DialogHost { get; private set; }

    public IEntityMotor Motor { get; private set; }
    public EntityTuning Tuning { get; private set; }
    public GameObject SelfObject { get; private set; }

    public ColonyState Colony { get; private set; }
    public int ColonyIndex => Colony != null ? Colony.Index : 0;
    public int ColonySlot = -1;
    public NestState Nest { get; private set; }
    public MovePlanner Planner { get; private set; }
    public MortalityComponent Mortality { get; private set; }

    public int EntityId { get; private set; }
    public void AssignId(int id) => EntityId = id;
    public ParentRef Parent { get; private set; }
    public Caste Caste { get; private set; } = Caste.Worker;

    private InstinctInput _instinctInput;
    public ref readonly InstinctInput InstinctInput => ref _instinctInput;
    public InfectionService Infection { get; private set; }

    public InputVectorizer Vectorizer;

    public int MimickedActionIndex { get; private set; } = -1;
    public void SetMimickedAction(int index) => MimickedActionIndex = index;
    public void ConsumeMimickedAction() => MimickedActionIndex = -1;

    public float[] LastInput;
    public int LastActionIndex;
    public EntityAction LastAction { get; private set; }
    public void SetLastAction(int index)
    {
        LastActionIndex = index;
        LastAction = (EntityAction)index;
        if ((uint)index < (uint)ActionCount) _actionTimes[index] = SimulationClock.TimeD;
    }

    private readonly double[] _actionTimes = CreateActionTimes();
    public double[] ActionTimes => _actionTimes;

    private static double[] CreateActionTimes()
    {
        var t = new double[ActionCount];
        for (int i = 0; i < t.Length; i++) t[i] = double.NegativeInfinity;
        return t;
    }

    public float SinceAction(EntityAction action)
    {
        double t = _actionTimes[(int)action];
        return double.IsNegativeInfinity(t) ? float.MaxValue : (float)(SimulationClock.TimeD - t);
    }

    public float TimeAlive { get; private set; }
    public int FoodEaten { get; private set; }
    public int ReproduceCount { get; private set; }
    public float DamageDealt { get; private set; }
    public float FinalFitness { get; private set; }

    public int PlanSlot = -1;
    private float _planSide = 1f;
    public float NextPlanSide() => _planSide = -_planSide;

    public float Carrying { get; private set; }
    public void Carry(float energy) => Carrying += math.max(0f, energy);
    public float TakeCarried()
    {
        float c = Carrying;
        Carrying = 0f;
        return c;
    }

    private bool _receivedFood;
    public void ReceiveFood(float energy)
    {
        Digestion?.Ingest(energy);
        _receivedFood = true;
    }

    public bool ConsumeReceivedFood()
    {
        bool r = _receivedFood;
        _receivedFood = false;
        return r;
    }

    private double _warnedUntil = double.NegativeInfinity;
    private double _lastDamageTime = double.NegativeInfinity;
    public bool IsWarned => SimulationClock.TimeD < _warnedUntil;
    public void Warn(double until) { if (until > _warnedUntil) _warnedUntil = until; }

    public bool IsAtNest { get; private set; }
    public bool IsInDanger =>
        SimulationClock.TimeD - _lastDamageTime < SimulationRules.Frame.WarnWindow
        || _lastDanger >= SimulationRules.Frame.WarnDangerThreshold;

    public float2 Position2D => Extension.Flat(Motor.Position());

    public float SpeedMul
    {
        get
        {
            ref readonly var r = ref SimulationRules.Frame;
            float lethargy = Virology != null ? Virology.Modifiers.Lethargy : 0f;
            float mul = (1f - lethargy * r.LethargySlow) * (Carrying > 0f ? r.CarrySpeedMul : 1f);
            return math.max(mul, 0.1f);
        }
    }

    public float AttackDamageMul
        => 1f + (Virology != null ? Virology.Modifiers.Frenzy : 0f) * SimulationRules.Frame.FrenzyDamageMul;

    public float BodyEnergy => Stats.En + (Digestion != null ? Digestion.Stomach : 0f) + Carrying;

    private R3.ReactiveProperty<float> _vecMaxHealth;
    private R3.ReactiveProperty<float> _vecMaxEnergy;

    private TerrainSensor _terrain;
    private ChunkFoodIndex _foodIndex;
    private TerrainSample _lastTerrain = TerrainSample.Invalid;

    public ref readonly TerrainSample LastTerrain => ref _lastTerrain;

    public float2 DeathPosition;
    public bool IsDeathPending { get; private set; }
    public void MarkDeathPending() => IsDeathPending = true;

    public int ManagerIndex = -1;

    private readonly float[] _decisionInput = new float[InputVectorizer.VectorSize];
    public float[] DecisionInput => _decisionInput;

    private EntitySpatialGrid _grid;
    private static readonly EntityModel[] Neighbors = new EntityModel[16];

    private long  _foodCell;
    private float _foodDistance = -1f;
    private bool  _foodValid;

    public bool  CachedFoodValid    => _foodValid;
    public long  CachedFoodCell     => _foodCell;
    public float CachedFoodDistance => _foodDistance;
    public void  InvalidateCachedFood() => _foodValid = false;

    private EntityModel _nearest;
    private float       _nearestDistance = -1f;

    public EntityModel CachedNearest =>
        _nearest != null && !_nearest.IsDisposed && !_nearest.IsDeathPending ? _nearest : null;
    public float CachedNearestDistance => _nearestDistance;

    private readonly IEntityCommand[] _commands = new IEntityCommand[ActionCount];
    private EntityCommandRunner _runner;

    private float _fitnessCache;
    private float _fitnessCacheTime = float.NaN;
    private int   _fitnessEpoch;
    private int   _fitnessCacheEpoch = -1;

    private double    _attackReadyTime;
    private double    _lastCycleTime;
    private DayCycle  _dayCycle;
    private int       _crowd;
    private float     _lastDanger;

    private double _driveIntegral;
    private double _decisionIntegral;
    private double _decisionStart;
    private float  _decisionDrive;

    public bool TryStartAttackCooldown()
    {
        double now = SimulationClock.TimeD;
        if (now < _attackReadyTime) return false;
        _attackReadyTime = now + Tuning.AttackCooldown;
        return true;
    }

    public override void SetUp()
    {
        _lastCycleTime   = SimulationClock.TimeD;
        _attackReadyTime = 0d;
        BeginHomeostasis();
    }

    public void RegisterFitnessEvent(FitnessEvent evt, float amount = 0f)
    {
        switch (evt)
        {
            case FitnessEvent.FoodEaten: FoodEaten++; break;
            case FitnessEvent.Reproduced: ReproduceCount++; break;
            case FitnessEvent.DamageDealt: DamageDealt += amount; break;
        }
        _fitnessEpoch++;
    }

    public float GetFitness()
    {
        if (_fitnessCacheEpoch == _fitnessEpoch && _fitnessCacheTime == TimeAlive)
            return _fitnessCache;

        ref readonly var rules = ref SimulationRules.Frame;

        float life     = TimeAlive;
        float survival = rules.FitnessSurvivalWeight
                       * (1f - math.exp(-life / math.max(rules.FitnessSurvivalTau, 1e-3f)));

        float invTime = rules.FitnessRateWindow / math.max(life, rules.FitnessMinLifetime);

        float events = FoodEaten      * rules.FitnessFoodRateWeight
                     + ReproduceCount * rules.FitnessReproduceRateWeight
                     + DamageDealt    * rules.FitnessDamageRateWeight;

        _fitnessCache      = survival + events * invTime;
        _fitnessCacheTime  = TimeAlive;
        _fitnessCacheEpoch = _fitnessEpoch;
        return _fitnessCache;
    }

    public void CaptureFinalFitness() => FinalFitness = GetFitness();

    public event Action<EntityModel> OnReproduceRequest;
    public void RequestReproduce() => OnReproduceRequest?.Invoke(this);

    public ChunkFoodIndex FoodIndex => _foodIndex;

    public void Configure(
        ColonyState colony, EntityBrainContext ctx,
        IEntityMotor motor, IEntityDeathHandler death,
        GameObject selfObject, EntityTuning baseTuning, EntityModel parent = null)
    {
        Colony = colony;
        Parent = new ParentRef(parent);
        var brain  = colony.Brain;
        var tuning = EntityTuning.Express(in baseTuning, in ctx.CoordMLP.Params, CasteModifiers.For(Caste));
        Motor = motor;
        SelfObject = selfObject;
        Tuning = tuning;
        DialogHost = (IEntityDialogHost)motor;

        Stats = GetOrCreateEntityComponent<StatsComponent>();
        Stats.FillComponent(tuning.MaxHealth, tuning.MaxEnergy);

        Digestion = GetOrCreateEntityComponent<DigestionComponent>();
        Digestion.Configure(tuning.MaxEnergy * SimulationRules.Active.StomachCapacityFraction);

        ServiceLocator.Services.TryGet(out _grid);
        AddComponentToEntity(new PerceptionComponent(tuning.DetectionRadius, _grid));
        Perception = GetEntityComponent<PerceptionComponent>();

        _terrain = new TerrainSensor(ServiceLocator.GetService<MapGenerator>());
        ServiceLocator.Services.TryGet(out _foodIndex);

        ServiceLocator.Services.TryGet(out MovePlanner planner);
        ServiceLocator.Services.TryGet(out InfectionService infection);
        Nest      = colony.Nest;
        Planner   = planner;
        Infection = infection;

        Speech = GetOrCreateEntityComponent<SpeechComponent>();
        Speech.Inject((IEntityAudio)motor);
        Points = GetOrCreateEntityComponent<PointsOfInterestComponent>();

        AddComponentToEntity(new BrainComponent(brain, ctx));
        Brain = GetEntityComponent<BrainComponent>();

        _runner = new EntityCommandRunner(this);

        AddComponentToEntity(new MortalityComponent(Stats.Health));
        Mortality = GetEntityComponent<MortalityComponent>();
        Mortality.Died += InterruptCommand;
        Mortality.Died += () => death?.OnDeath();

        _vecMaxHealth = new R3.ReactiveProperty<float>(tuning.MaxHealth);
        _vecMaxEnergy = new R3.ReactiveProperty<float>(tuning.MaxEnergy);
        Vectorizer = new InputVectorizer(_vecMaxHealth, _vecMaxEnergy);

        Virology = GetOrCreateEntityComponent<VirologyComponent>();
        uint seed = (uint)RavineRandom.RangeInt(1, int.MaxValue);
        var parentVirology = parent?.Virology;
        if (parentVirology != null && parentVirology.IsCreated && !parentVirology.IsDisposed)
            Virology.FillFromParent(parentVirology, in ctx.CoordMLP.Params, seed);
        else
            Virology.FillComponent(in ctx.CoordMLP.Params, seed);
    }

    public override void Init()
    {
        _commands[(int)EntityAction.Idle]          = new IdleCommand(this);
        _commands[(int)EntityAction.Wander]        = new WanderCommand(this);
        _commands[(int)EntityAction.RememberPoint] = new RememberPointCommand(this);
        _commands[(int)EntityAction.GoToPoint]     = new GoToPointCommand(this);
        _commands[(int)EntityAction.Attack]        = new AttackCommand(this);
        _commands[(int)EntityAction.Flee]          = new FleeCommand(this);
        _commands[(int)EntityAction.Eat]           = new EatCommand(this);
        _commands[(int)EntityAction.Reproduce]     = new ReproduceCommand(this);
        _commands[(int)EntityAction.Speech]        = new SpeechCommand(this);
        _commands[(int)EntityAction.Mimic]         = new MimicCommand(this);
        _commands[(int)EntityAction.Rest]          = new RestCommand(this);
        _commands[(int)EntityAction.Threaten]      = new ThreatenCommand(this);
        _commands[(int)EntityAction.ShareFood]     = new ShareFoodCommand(this);
        _commands[(int)EntityAction.ApproachFood]  = new ApproachFoodCommand(this);
        _commands[(int)EntityAction.ReturnNest]    = new ReturnNestCommand(this);
        _commands[(int)EntityAction.PickUp]        = new PickUpCommand(this);
        _commands[(int)EntityAction.StoreFood]     = new StoreFoodCommand(this);
    }

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

    private bool  _cycleActive;
    private float _cycleNow;
    private float _cycleDt;

    public bool CycleActive => _cycleActive;

    public override void UpdateEntityCycle()
    {
        Brain.BeginBatch();
        if (!BeginCycle()) return;
        SubmitDecision(true);
        Brain.RunBatch();
        EndCycle();
    }

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
        if (Stats.IsDisposed || Stats.Health.Value <= 0f) return false;

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
        if (dt <= 0f) dt = 1f / 60f;
        if (dt > rules.MaxCycleDt) dt = rules.MaxCycleDt;
        float now = (float)nowD;

        TimeAlive += dt;

        float dayPhase = ResolveDayPhase();
        bool  night    = IsNightPhase(dayPhase);

        var host = new HostState
        {
            HpFraction     = Stats.Hp / Stats.MaxHealth,
            Night          = night,
            Crowd          = _crowd,
            WeakThreshold  = rules.SkipWeakHpFraction,
            CrowdThreshold = rules.SkipCrowdCount,
        };
        float hpBeforeVirus = Stats.Hp;
        Virology.Step(Stats, dt, in host);
        if (Stats.Hp < hpBeforeVirus) Mortality.NoteHarm(DeathCause.Virus, Stats.Hp);
        ref var mods = ref Virology.Modifiers;
        if (mods.ForceSpeechRequested) Broadcast(Speech.Own);
        if (!IsAliveForTick()) return false;

        Vector3 pos  = Motor.Position();
        float2  self = new float2(pos.x, pos.z);
        float detect = Tuning.DetectionRadius * math.max(0.1f, 1f - mods.Blind * rules.BlindStrength);

        _nearest = Perception.FindNearestEntity(pos, this, detect, out _nearestDistance);
        _crowd   = CountAndRelease(Perception.FindEntitiesInRadius(pos, this, rules.CrowdRadius, Neighbors));

        _foodValid    = false;
        _foodDistance = -1f;
        if (_foodIndex != null &&
            _foodIndex.TryFindNearestFood(pos.x, pos.z, detect, out _foodCell, out _foodDistance))
            _foodValid = true;

        FoodKind foodKind = FoodKind.Plant;
        bool foodInfected = false;
        float2 foodPos = float2.zero;
        if (_foodValid)
        {
            foodPos = ChunkFoodIndex.CellCenter(_foodCell);
            _foodIndex.TryGetKind(_foodCell, out foodKind, out foodInfected);
        }

        var nest = Nest;
        IsAtNest = nest != null && nest.Contains(self);
        if (nest != null && _foodValid) nest.Mark(ColonyChannel.Food, foodPos, rules.NestFoodMark * dt);

        if (!_terrain.TrySample(pos.x, pos.z, out _lastTerrain))
            _lastTerrain = TerrainSample.Invalid;

        _lastDanger = ComputeDangerLevel();

        float neighborLoad = 0f;
        var nearest = CachedNearest;
        if (nearest != null) nearest.Virology.TryGetViralInputs(out neighborLoad, out _, out _);

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
            Speech            = Speech.Heard,
            MimickedAction    = MimickedActionIndex,
            Now               = nowD,
        };
        Virology.TryGetViralInputs(out frame.ViralLoad, out frame.ViralSegments, out frame.ViralNet);

        LastInput = Vectorizer.Vectorize(in frame, _actionTimes, in _lastTerrain);

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
                    * (1f + mods.Fever * rules.FeverDrainMul);

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

        var brainCtx = Brain.Context;
        brainCtx.CoordBias[0] = mods.WanderBias;
        brainCtx.CoordBias[1] = mods.HuntBias + mods.Frenzy * rules.FrenzyHuntBias;
        brainCtx.CoordBias[2] = mods.ForageBias;
        brainCtx.CoordBias[3] = mods.SocialBias;
        brainCtx.FleeBias     = mods.FleeBias - mods.Frenzy * rules.FrenzyFleeSuppress;

        if (nest != null && nest.Alarm > 0f)
        {
            float alarm = nest.Alarm;
            if (nest.AlarmHunt) brainCtx.CoordBias[1] += alarm * rules.AlarmHuntBias;
            else
            {
                brainCtx.CoordBias[0] += alarm * rules.AlarmSurviveBias;
                brainCtx.FleeBias     += alarm * rules.AlarmFleeBias;
            }
        }

        brainCtx.EnergyNorm = Stats.En / Stats.MaxEnergy;

        _instinctInput = new InstinctInput
        {
            HpFraction     = Stats.Hp / Stats.MaxHealth,
            EnergyFraction = Stats.En / Stats.MaxEnergy,
            Stomach        = frame.Stomach,
            Carrying       = frame.Carrying,
            LocalDanger    = frame.LocalDanger,
            Alarm          = frame.Alarm,
            EntityDistance = _nearestDistance,
            FoodDistance   = _foodValid ? _foodDistance : -1f,
            AtNest         = IsAtNest ? (byte)1 : (byte)0,
            Night          = night ? (byte)1 : (byte)0,
            Warned         = IsWarned ? (byte)1 : (byte)0,
        };

        _cycleNow    = now;
        _cycleDt     = dt;
        _cycleActive = true;
        return true;
    }

    private static int CountAndRelease(int found)
    {
        for (int i = 0; i < found; i++) Neighbors[i] = null;
        return found;
    }

    public float Drive()
    {
        ref readonly var r = ref SimulationRules.Frame;
        float stored = (Digestion != null ? Digestion.Stomach : 0f) + Carrying;
        float en = math.saturate((Stats.En + stored * r.DigestEffActive) / Stats.MaxEnergy);
        float hp = math.saturate(Stats.Hp / Stats.MaxHealth);
        float de = 1f - en, dh = 1f - hp;
        return math.sqrt(r.DriveEnergyWeight * de * de + r.DriveHealthWeight * dh * dh);
    }

    private void BeginHomeostasis()
    {
        _decisionStart    = SimulationClock.TimeD;
        _decisionIntegral = _driveIntegral;
        _decisionDrive    = Stats != null && !Stats.IsDisposed ? Drive() : 0f;
    }

    public float HomeostaticReturn()
    {
        if (Stats == null || Stats.IsDisposed) return 0f;
        ref readonly var r = ref SimulationRules.Frame;

        float elapsed  = (float)(SimulationClock.TimeD - _decisionStart);
        float integral = (float)(_driveIntegral - _decisionIntegral);
        float shaping  = _decisionDrive - DelayedPerceptron.DiscountTime(r.ExecGammaPerSecond, elapsed) * Drive();
        return -r.DriveRewardWeight * integral + r.DriveShapingWeight * shaping;
    }

    public void TakeDamage(float amount, EntityModel source, DeathCause cause = DeathCause.Killed)
    {
        if (amount <= 0f || Stats == null || Stats.IsDisposed) return;
        ref readonly var r = ref SimulationRules.Frame;

        if (IsAtNest && LastAction == EntityAction.Rest) amount *= r.NestRestDamageMul;
        Mortality?.NoteHarm(cause, Stats.Hp - amount);
        Stats.Hp -= amount;
        _lastDamageTime = SimulationClock.TimeD;

        var nest = Nest;
        if (nest == null) return;
        float norm = amount / Stats.MaxHealth;
        nest.Mark(ColonyChannel.Danger, Position2D, norm * r.NestDangerMark);
        if (IsAtNest) nest.RaiseAlarm(norm * r.AlarmFromDamage);
    }

    public void Broadcast(float4 speech)
    {
        Speech.SetOwn(speech);
        if (Perception == null) return;

        ref readonly var r = ref SimulationRules.Frame;
        bool warn = IsInDanger;
        double until = SimulationClock.TimeD + r.WarnWindow;

        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, r.SpeechRadius, Neighbors);
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;
            e.Speech.ReceiveVector(speech);
            if (warn) e.Warn(until);
        }
    }

    private static float2 DirectionTo(in float2 from, in float2 to)
    {
        float2 d  = to - from;
        float  l2 = math.lengthsq(d);
        return l2 > 1e-6f ? d * math.rsqrt(l2) : float2.zero;
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
            SetLastAction(decision.Action);
            StartDecision(in decision);
        }

        OnUpdate.Execute(R3.Unit.Default);
    }

    private void StartDecision(in BrainDecision decision)
    {
        _runner.Interrupt();
        BeginHomeostasis();

        int a   = decision.Action;
        var cmd = (uint)a < (uint)_commands.Length ? _commands[a] : null;

        var r = SimulationRules.Active;
        if (cmd == null)
        {
            Brain.CompleteDecision(in decision, r.FailureReward, SimulationClock.Time, EntityCommandStatus.Failed);
            return;
        }

        if (!cmd.CanExecute())
        {
            float penalty = r.InfeasibleActionHealthPenalty;
            if (penalty > 0f) Stats.Hp -= penalty;
            Brain.CompleteDecision(in decision, r.InfeasibleActionReward, SimulationClock.Time, EntityCommandStatus.Failed);
            return;
        }

        _runner.Start(cmd, in decision, CommandSource.Brain);
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

    private void InterruptCommand() => _runner?.Interrupt();

    public override void DeepClean()
    {
        _runner?.Abandon();
        Planner?.Cancel(this);
        _nearest = null;
        Vectorizer?.Dispose();
        Vectorizer = null;
        _vecMaxHealth?.Dispose();
        _vecMaxEnergy?.Dispose();
        _vecMaxHealth = null;
        _vecMaxEnergy = null;
    }
}
