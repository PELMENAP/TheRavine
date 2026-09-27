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
    public uint InstinctLatches { get; private set; }
    public bool IsHungry => (InstinctLatches & InstinctBits.Hungry) != 0;
    public bool IsSated  => Digestion != null && Digestion.Fill >= Brain.Context.CoordMLP.Params.SatedFill;

    private PlanRunner _plan;
    public PlanRunner Plan => _plan;
    private bool _hasThreat;
    public PlanHint PlanHints => ComputePlanHints(_hasThreat);

    public float Stress { get; private set; }
    public void AddStress(float amount) => Stress = math.saturate(Stress + math.max(0f, amount));

    private float _extrinsicReward;
    public void AddExtrinsicReward(float reward) => _extrinsicReward += reward;
    public float ConsumeExtrinsicReward()
    {
        float r = _extrinsicReward;
        _extrinsicReward = 0f;
        return r;
    }

    public bool IsCommandRunning => _runner != null && _runner.IsRunning;
    public CommandSource CurrentSource => _runner != null ? _runner.CurrentSource : CommandSource.Brain;
    public bool IsAliveForPlan => IsAliveForTick();
    public ulong LineageId => Virology != null ? Virology.EndogenousLineage : 0UL;
    public float CarryCapacity => Stats.MaxEnergy * SimulationRules.Frame.StomachCapacityFraction * _casteMods.Carry;

    private CasteModifiers _casteMods = CasteModifiers.Neutral;
    public ref readonly CasteModifiers CasteMods => ref _casteMods;
    public bool IsWild => Colony != null && Colony.IsWild;

    private readonly float2[] _dirVec    = new float2[(int)DirectionKind.Count];
    private readonly float[]  _dirRadius = new float[(int)DirectionKind.Count];
    private byte  _dirMask;
    private float _ouAngle;
    public byte  DirectionMask => _dirMask;
    public float OuAngle => _ouAngle;

    private readonly float[] _planRewardEma = new float[PlanCatalog.Count];
    public float PlanRewardEma(PlanKind plan) => plan < PlanKind.Count ? _planRewardEma[(int)plan] : 0f;

    private float _dopamine, _cortisol, _oxytocin, _ghrelin;
    public float Dopamine => _dopamine;
    public float Cortisol => _cortisol;
    public float Oxytocin => _oxytocin;
    public float Ghrelin  => _ghrelin;
    private int   _kinCrowd;

    public float MaxAge { get; private set; } = float.MaxValue;
    public float AgeFraction => TimeAlive / math.max(MaxAge, 1f);
    public double NextCasteReview;
    private float _immuneUpkeepMul = 1f;

    private bool _sick;
    public bool IsSick => _sick;
    public bool IsNight => _night;
    private bool _kinSick;
    private bool _onForeignTerritory;
    private float _season;
    private float _winterApproach;
    private bool  _winter;

    private float2 _recruitTarget;
    private double _recruitUntil = double.NegativeInfinity;
    public bool   IsRecruited   => SimulationClock.TimeD < _recruitUntil;
    public float2 RecruitTarget => _recruitTarget;
    public void   ClearRecruit() => _recruitUntil = double.NegativeInfinity;
    public void Recruit(float2 target, double until)
    {
        _recruitTarget = target;
        _recruitUntil  = until;
    }

    private float2 _foodSite;
    private bool   _hasFoodSite;
    public void NoteFoodSite(float2 p)
    {
        _foodSite    = p;
        _hasFoodSite = true;
    }
    public bool TryGetFoodSite(out float2 site)
    {
        site = _foodSite;
        return _hasFoodSite;
    }

    public float StoredTotal { get; private set; }
    public void RecordStored(float amount) => StoredTotal += math.max(0f, amount);

    private UtilityFeatures _utility;
    public ref readonly UtilityFeatures UtilityInput => ref _utility;

    private FoodKind _foodKind;

    private CasteThresholdSystem _thresholds;

    private double _lastAlarmCry = double.NegativeInfinity;

    private EntityTuning _baseTuning;
    public bool   CasteFixed { get; private set; }
    public double LastCasteShift = double.NegativeInfinity;
    public bool   IsJuvenile => TimeAlive < SimulationRules.Frame.JuvenileTime;
    public float  RestHealMul { get; private set; } = 1f;

    public bool IsLeader { get; private set; }
    public void SetLeader(bool leader)
    {
        IsLeader = leader;
        Brain?.Context.SetPositiveAdvantageScale(leader ? SimulationRules.Active.LeaderPositiveAdvMul : 1f);
    }

    public float2 Heading { get; private set; }
    private float2 _lastPos;
    private bool   _hasLastPos;

    private BoidOutput _flock;
    public float2 FlockSteer => _flock.Steer;
    public void SetFlock(in BoidOutput flock) => _flock = flock;

    public BoidAgent BuildBoidAgent()
    {
        var g = Brain.Context.CoordMLP.Params;
        var aux = Brain.Context.CoordAux;
        float isolate = Virology != null ? Virology.Modifiers.Isolate * SimulationRules.Frame.IsolateStrength : 0f;
        float social  = math.max(0f, (1f + aux.x) * (1f - isolate));
        float spacing = (1f + aux.y) * (1f + isolate);
        return new BoidAgent
        {
            Position = Position2D,
            Heading  = Heading,
            Weights  = IsWild ? float3.zero : new float3(g.BoidSeparation * spacing, g.BoidAlignment * social, g.BoidCohesion * social),
            Colony   = ColonyIndex,
            Active   = (byte)(IsDisposed || IsDeathPending ? 0 : 1),
            Plan     = (byte)(_plan != null ? _plan.Kind : PlanKind.Count),
            Night    = (byte)(_night ? 1 : 0),
        };
    }

    private EntityModel _nearestForeign;
    private float       _nearestForeignDistance = -1f;
    private PlayerPresence _players;
    private bool      _playerValid;
    private float2    _playerPos;
    private float     _playerDistance = -1f;
    private Transform _playerTransform;
    private bool      _night;

    public bool  PlayerNear     => _playerValid;
    public float PlayerDistance => _playerDistance;
    public PlayerPresence Players => _players;

    public bool IsFrenzied => Virology != null && Virology.Modifiers.BiteSeek >= SimulationRules.Frame.BiteSeekOwnThreshold;

    private EntityModel ValidForeign
    {
        get
        {
            var f = _nearestForeign;
            return f != null && !f.IsDisposed && !f.IsDeathPending ? f : null;
        }
    }

    public HuntTarget CachedHuntTarget
    {
        get
        {
            var foreign = ValidForeign;
            bool player = _playerValid && _playerTransform != null;
            if (foreign != null && (!player || _nearestForeignDistance <= _playerDistance)) return new HuntTarget(foreign, null);
            if (player) return new HuntTarget(null, _playerTransform);
            var nearest = CachedNearest;
            return nearest != null && IsFrenzied ? new HuntTarget(nearest, null) : default;
        }
    }

    private float2[] _trail;
    private int      _trailHead;
    private int      _trailCount;
    private double   _nextTrailSample;

    public void DepositTrail()
    {
        var nest = Nest;
        if (nest == null || _trail == null) return;
        float amount = SimulationRules.Frame.TrailDeposit;
        int cap = _trail.Length;
        for (int i = 0; i < _trailCount; i++)
            nest.Mark(ColonyChannel.Trail, _trail[(_trailHead - 1 - i + cap) % cap], amount);
        _trailCount = 0;
    }

    public void ShareMemoryWithColony()
    {
        var colony = Colony;
        if (colony == null) return;
        for (int i = 0; i < Points.Count; i++)
        {
            ref readonly var p = ref Points.At(i);
            colony.Pois.Offer(p.Position, p.EnergyEma, 0f);
        }
    }

    public void AssignCaste(Caste caste)
    {
        Caste      = caste;
        CasteFixed = true;
        var mods   = CasteModifiers.For(caste);
        _casteMods = mods;
        RestHealMul = mods.RestHeal;

        var tuning = EntityTuning.Express(in _baseTuning, in Brain.Context.CoordMLP.Params, in mods);
        Tuning = tuning;
        Stats.Rescale(tuning.MaxHealth, tuning.MaxEnergy);
        Digestion.Resize(tuning.MaxEnergy * SimulationRules.Active.StomachCapacityFraction);
        if (_vecMaxHealth != null) _vecMaxHealth.Value = tuning.MaxHealth;
        if (_vecMaxEnergy != null) _vecMaxEnergy.Value = tuning.MaxEnergy;
        Brain.Context.CasteMask = ActionCatalog.CasteActionMask(caste);
    }
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
    private float2 _warnSource;
    public bool IsWarned => SimulationClock.TimeD < _warnedUntil;
    public void Warn(double until, float2 source)
    {
        if (until <= _warnedUntil) return;
        _warnedUntil = until;
        _warnSource  = source;
    }

    private EntityModel _lastAttacker;
    private double      _lastAttackTime = double.NegativeInfinity;
    private bool        _threatResolved;
    private float2      _threatSource;

    public bool TryGetThreatSource(out float2 source)
    {
        source = _threatSource;
        return _threatResolved;
    }

    private EntityModel RecentAttacker
    {
        get
        {
            var a = _lastAttacker;
            if (a == null || a.IsDisposed || a.IsDeathPending) return null;
            return SimulationClock.TimeD - _lastAttackTime < SimulationRules.Frame.WarnWindow ? a : null;
        }
    }

    public bool HasDirectThreat => RecentAttacker != null || ValidForeign != null || _playerValid;

    private bool  _nestFood;
    public bool   CanEatFromNest => _nestFood;

    public bool IsAtNest { get; private set; }

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

    public float EffectiveEnergyNorm
        => (Stats.En + (Digestion != null ? Digestion.Stomach : 0f) * SimulationRules.Frame.DigestEffActive) / Stats.MaxEnergy;

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

    public bool AttackReady => SimulationClock.TimeD >= _attackReadyTime;

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
        _baseTuning = baseTuning;
        _trail = new float2[math.max(1, SimulationRules.Active.TrailCapacity)];
        ctx.CasteMask = ActionCatalog.CasteActionMask(Caste);
        var rulesA = SimulationRules.Active;
        _casteMods = colony.IsWild ? CasteModifiers.Predator() : CasteModifiers.For(Caste);
        if (colony.IsWild) CasteFixed = true;
        var tuning = EntityTuning.Express(in baseTuning, in ctx.CoordMLP.Params, _casteMods);
        MaxAge = rulesA.BaseMaxAge * ctx.CoordMLP.Params.MaxAgeMul;
        _immuneUpkeepMul = 1f + ctx.CoordMLP.Params.ImmuneInvestment * rulesA.ImmuneUpkeep;
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
        ServiceLocator.Services.TryGet(out _players);
        ServiceLocator.Services.TryGet(out _thresholds);
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
        _plan   = new PlanRunner(this);

        AddComponentToEntity(new MortalityComponent(Stats.Health));
        Mortality = GetEntityComponent<MortalityComponent>();
        Mortality.Died += () =>
        {
            _plan.Interrupt();
            InterruptCommand();
        };
        Mortality.Died += () => death?.OnDeath();

        float startHealth = colony.IsWild ? tuning.MaxHealth : tuning.MaxHealth * rulesA.JuvenileHealthMul;
        if (!colony.IsWild) Stats.Rescale(startHealth, tuning.MaxEnergy);

        _vecMaxHealth = new R3.ReactiveProperty<float>(startHealth);
        _vecMaxEnergy = new R3.ReactiveProperty<float>(tuning.MaxEnergy);
        Vectorizer = new InputVectorizer(_vecMaxHealth, _vecMaxEnergy);

        Virology = GetOrCreateEntityComponent<VirologyComponent>();
        uint seed = (uint)RavineRandom.RangeInt(1, int.MaxValue);
        var parentVirology = parent?.Virology;
        if (parentVirology != null && parentVirology.IsCreated && !parentVirology.IsDisposed)
            Virology.FillFromParent(parentVirology, in ctx.CoordMLP.Params, seed);
        else
            Virology.FillComponent(in ctx.CoordMLP.Params, seed);
        Virology.SetImmuneStrength(math.lerp(rulesA.ImmuneThresholdMin, rulesA.ImmuneThresholdMax, ctx.CoordMLP.Params.ImmuneInvestment));
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
        _commands[(int)EntityAction.Follow]        = new FollowCommand(this);
        _commands[(int)EntityAction.MoveNest]      = new MoveNestCommand(this);
        _commands[(int)EntityAction.Groom]         = new GroomCommand(this);
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
        float detect = Tuning.DetectionRadius * math.max(0.1f, 1f - mods.Blind * rules.BlindStrength);

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

    private void UpdateHeading()
    {
        float2 p = Position2D;
        if (_hasLastPos)
        {
            float2 d = p - _lastPos;
            if (math.lengthsq(d) > 1e-6f)
                Heading = math.normalizesafe(math.lerp(Heading, math.normalize(d), SimulationRules.Frame.HeadingEmaAlpha));
        }
        _lastPos    = p;
        _hasLastPos = true;
    }

    private void SampleTrail(in float2 self, double now, in SimulationRules.RulesFrame r)
    {
        if (_trail == null || now < _nextTrailSample) return;
        _nextTrailSample = now + r.TrailSampleInterval;
        _trail[_trailHead] = self;
        _trailHead = (_trailHead + 1) % _trail.Length;
        if (_trailCount < _trail.Length) _trailCount++;
    }

    public EntityModel FindNearbyJuvenile(float radius)
    {
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, ForeignScratch);
        EntityModel best = null;
        float bestFill = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var e = ForeignScratch[i];
            ForeignScratch[i] = null;
            if (e == null || e.Colony != Colony || !e.IsJuvenile) continue;
            float fill = e.Digestion.Fill;
            if (fill >= bestFill) continue;
            bestFill = fill;
            best     = e;
        }
        return best;
    }

    private static readonly EntityModel[] ForeignScratch = new EntityModel[16];
    private static ColonyRegistry _registry;

    private bool HasForeignEntities()
    {
        int populated = 0;
        for (int c = 0; c < _registry.Count; c++)
            if (_registry[c].MemberCount > 0 && ++populated > 1) return true;
        return false;
    }

    public void ApplyUtilityPrior(ReadOnlySpan<float> prior)
    {
        if (!_cycleActive || prior.Length < PlanCatalog.Count) return;
        float w = SimulationRules.Frame.UtilityPriorWeight;
        var bias = Brain.Context.CoordBias;
        for (int p = 0; p < PlanCatalog.Count; p++) bias[p] += w * prior[p];
    }

    private bool ComputeNestFood(NestState nest, in SimulationRules.RulesFrame r)
    {
        if (nest == null || !IsAtNest) return false;
        float storage = nest.Storage;
        if (storage < r.NestEatPortion * r.NestFoodMinPortionFraction) return false;
        if (storage >= r.NestJuvenileReserve || IsJuvenile || Caste == Caste.Nurse) return true;
        var colony = Colony;
        if (colony == null || colony.JuvenileCount == 0) return true;
        return FindNearbyJuvenile(nest.Radius) == null;
    }

    private bool ResolveThreatSource(in float2 self, float localDanger, in SimulationRules.RulesFrame r, out float2 source)
    {
        var attacker = RecentAttacker;
        if (attacker != null)
        {
            source = attacker.Position2D;
            return true;
        }

        var foreign = ValidForeign;
        if (foreign != null && (!_playerValid || _nearestForeignDistance <= _playerDistance))
        {
            source = foreign.Position2D;
            return true;
        }
        if (_playerValid)
        {
            source = _playerPos;
            return true;
        }

        if (IsWarned)
        {
            source = _warnSource;
            return true;
        }

        source = default;
        var nest = Nest;
        if (nest == null || localDanger <= r.ThreatMinDanger) return false;

        float radius = Tuning.DetectionRadius * r.FleeDistanceMul;
        float spread = nest.SampleDirection(self, radius, r.WanderGradientSamples, 0f, 0f, -1f, out float2 toDanger);
        if (spread <= r.WanderGradientMin) return false;
        source = self + toDanger * radius;
        return true;
    }

    private EntityModel FindNearestForeign(Vector3 pos, float radius, out float distance)
    {
        distance = -1f;
        if (_registry == null) ServiceLocator.Services.TryGet(out _registry);
        if (_registry == null || !HasForeignEntities()) return null;

        int found = Perception.FindEntitiesInRadius(pos, this, radius, ForeignScratch);
        EntityModel best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var e = ForeignScratch[i];
            ForeignScratch[i] = null;
            if (e == null || e.Colony == Colony) continue;
            float d = math.distancesq((float3)e.Motor.Position(), (float3)pos);
            if (d >= bestD) continue;
            bestD = d;
            best  = e;
        }
        if (best != null) distance = math.sqrt(bestD);
        return best;
    }

    public void ApplyInstinct(in InstinctOutput output)
    {
        InstinctLatches = output.Latches;
        if (!_cycleActive) return;

        if (output.Interrupt != 0 || output.Reflex != InstinctBits.NoReflex) _plan.Interrupt();
        if (output.Reflex == InstinctBits.NoReflex)
        {
            TrySummit();
            return;
        }

        int action = output.Reflex;
        float2 heading = float2.zero;
        var nest = Nest;
        if ((output.Mask & InstinctBits.Sick) != 0 && nest != null)
            heading = math.normalizesafe(Position2D - nest.Position, new float2(0f, 1f));
        var reflex = new BrainDecision(action, 0, 0, Brain.CurrentGoal, _cycleNow,
            ActionDurationTable.Max(action), in heading);
        TryStartCommand(action, in reflex, CommandSource.Instinct);
    }

    private void TrySummit()
    {
        ref readonly var r = ref SimulationRules.Frame;
        if (Virology == null || Virology.Modifiers.Summit < r.SummitThreshold) return;
        if (Stats.Hp / Stats.MaxHealth >= r.SummitHpFraction || !_lastTerrain.IsValid) return;
        if (_runner.IsRunning && _runner.CurrentSource == CommandSource.Instinct) return;

        float2 uphill = math.normalizesafe(new float2(_lastTerrain.GradX, _lastTerrain.GradZ));
        if (math.lengthsq(uphill) < 1e-6f) return;

        _plan.Interrupt();
        int action = (int)EntityAction.Wander;
        var climb = new BrainDecision(action, 0, 0, Brain.CurrentGoal, _cycleNow,
            ActionDurationTable.Max(action), in uphill);
        TryStartCommand(action, in climb, CommandSource.Instinct);
    }

    public bool TryStartCommand(int action, in BrainDecision decision, CommandSource source)
    {
        var cmd = (uint)action < (uint)_commands.Length ? _commands[action] : null;
        if (cmd == null) return false;

        if (!cmd.CanExecute())
        {
            float penalty = SimulationRules.Active.InfeasibleActionHealthPenalty;
            if (source == CommandSource.Brain && penalty > 0f) Stats.Hp -= penalty;
            return false;
        }

        SetLastAction(action);
        _runner.Start(cmd, in decision, source);
        return true;
    }

    public void OnCommandFinished(CommandSource source, EntityCommandStatus status, float reward, int action)
    {
        if (source == CommandSource.Instinct) return;
        _plan.OnStepFinished(status, reward, action);
    }

    private static int CountAndRelease(int found, ColonyState colony, out int kin)
    {
        kin = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e != null && e.Colony == colony) kin++;
        }
        return found;
    }

    private static int CountAndRelease(int found)
    {
        for (int i = 0; i < found; i++) Neighbors[i] = null;
        return found;
    }

    private float SenescenceMul(in SimulationRules.RulesFrame r)
    {
        float onset = math.saturate(r.SenescenceOnset);
        float over  = math.saturate((AgeFraction - onset) / math.max(1f - onset, 1e-3f));
        return 1f + r.SenescenceUpkeep * over;
    }

    private void UpdateOrnsteinUhlenbeck(float dt, in SimulationRules.RulesFrame r)
    {
        float u1 = RavineRandom.RangeFloat(1e-4f, 1f);
        float u2 = RavineRandom.RangeFloat(0f, 1f);
        float n  = math.sqrt(-2f * math.log(u1)) * math.cos(2f * math.PI * u2);
        _ouAngle += -_ouAngle * dt / math.max(r.OuTau, 1e-3f) + r.OuSigma * math.sqrt(dt) * n;
    }

    private void UpdateHormones(float dt, in SimulationRules.RulesFrame r)
    {
        float k = 1f - math.exp(-dt / math.max(r.HormoneTau, 1e-3f));
        _cortisol = math.lerp(_cortisol, Stress, k);
        _oxytocin = math.lerp(_oxytocin, math.saturate(_kinCrowd / math.max(r.OxytocinNorm, 1e-3f)), k);
        _ghrelin  = math.lerp(_ghrelin, 1f - math.saturate(EffectiveEnergyNorm), k);
    }

    private float TemperatureScale(EntityBrainContext ctx, in SimulationRules.RulesFrame r)
    {
        float x = -r.TempDopamine * _dopamine + r.TempCortisol * _cortisol + r.TempSurprise * ctx.Surprise;
        return math.clamp(math.exp(x), r.TempMin, r.TempMax);
    }

    public void OnPlanEnded(PlanKind plan, float total, bool achieved)
    {
        if (plan >= PlanKind.Count) return;
        ref readonly var r = ref SimulationRules.Frame;
        float a = r.PlanRewardEmaAlpha;
        _planRewardEma[(int)plan] = math.lerp(_planRewardEma[(int)plan], total, a);
        _dopamine = math.lerp(_dopamine, math.tanh(total), a);

        if (!CasteFixed || IsJuvenile || IsWild || _thresholds == null) return;
        if (r.PlanCasteDomain[(int)plan] != (int)Caste) return;
        _thresholds.Reinforce(ManagerIndex, Caste, achieved);
    }

    public float SelectionFitness
    {
        get
        {
            ref readonly var r = ref SimulationRules.Frame;
            float colony = Colony != null && !Colony.IsWild ? Colony.Score : 0f;
            float share  = StoredTotal / math.max(r.NestStorageNorm, 1e-3f);
            return r.FitnessPersonalWeight * GetFitness() + r.FitnessColonyWeight * (colony + share);
        }
    }

    private void UpdateTerritory(NestState nest, in float2 self, float dt, in SimulationRules.RulesFrame r)
    {
        _onForeignTerritory = false;
        if (IsWild || nest == null) return;

        var foreign = ValidForeign;
        if (foreign != null && nest.FieldAt(ColonyChannel.Territory, foreign.Position2D) > r.TerritoryThreshold)
            nest.RaiseAlarm(r.TerritoryAlarm * dt);

        if (_registry == null) return;
        for (int c = 0; c < _registry.Count; c++)
        {
            var other = _registry[c];
            if (other == Colony || other.IsWild) continue;
            if (other.Nest.FieldAt(ColonyChannel.Territory, self) <= r.TerritoryThreshold) continue;
            _onForeignTerritory = true;
            return;
        }
    }

    private void ComputeDirections(NestState nest, in float2 self, in float2 foodPos, in SimulationRules.RulesFrame r)
    {
        _dirMask = 0;
        if (_foodValid) SetDirectionTarget(DirectionKind.Food, in self, in foodPos, in r);
        if (nest != null && nest.HasFoodPeak) SetDirectionTarget(DirectionKind.NestFood, in self, nest.FoodPeak, in r);
        if (Points.TryPickBest(in self, out float2 own)) SetDirectionTarget(DirectionKind.OwnPoi, in self, in own, in r);
        var colony = Colony;
        if (colony != null && colony.Pois.TryPickBest(self, out float2 shared, out _))
            SetDirectionTarget(DirectionKind.ColonyPoi, in self, in shared, in r);

        if (nest != null)
        {
            float phase = RavineRandom.RangeFloat(0f, 2f * math.PI);
            nest.SampleExtreme(self, r.FrontierRadius, r.WanderGradientSamples, phase, ColonyChannel.Explored, true, out float2 frontier);
            SetDirection(DirectionKind.Frontier, in frontier, r.FrontierRadius);
            if (nest.MigrationPressure > r.MigrationMinPressure)
                SetDirection(DirectionKind.Migration, math.normalizesafe(nest.Migration), r.MigrateLegRadius);
        }

        if (_flock.Neighbors > 0) SetDirection(DirectionKind.Flock, in _flock.CentroidDir, r.BoidRadius * 2f);
        _dirMask |= 1 << (int)DirectionKind.Levy;
    }

    private void SetDirectionTarget(DirectionKind kind, in float2 self, in float2 target, in SimulationRules.RulesFrame r)
    {
        float2 d = target - self;
        float  l = math.length(d);
        if (l < 0.5f) return;
        SetDirection(kind, d / l, math.min(l, r.PlannerRadiusMax));
    }

    private void SetDirection(DirectionKind kind, in float2 dir, float radius)
    {
        if (math.lengthsq(dir) < 1e-6f) return;
        _dirVec[(int)kind]    = dir;
        _dirRadius[(int)kind] = radius;
        _dirMask |= (byte)(1 << (int)kind);
    }

    public bool TryResolveDirection(byte kind, float offset, out float2 dir, out float radius)
    {
        ref readonly var r = ref SimulationRules.Frame;
        dir    = float2.zero;
        radius = 0f;
        if (kind >= (byte)DirectionKind.Count) return false;

        if (kind == (byte)DirectionKind.Levy)
        {
            float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
            math.sincos(a, out float sa, out float ca);
            dir    = new float2(ca, sa);
            radius = math.min(LevyStep(in r), r.PlannerRadiusMax);
        }
        else
        {
            if ((_dirMask & (1 << kind)) == 0) return false;
            dir    = _dirVec[kind];
            radius = _dirRadius[kind];
        }

        math.sincos(offset + _ouAngle, out float s, out float c);
        dir = new float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
        return true;
    }

    public float LevyStep(in SimulationRules.RulesFrame r)
    {
        float alpha = math.max(r.LevyAlpha * _casteMods.LevyAlpha, 1e-3f);
        float u = RavineRandom.RangeFloat(1e-4f, 1f);
        return math.clamp(r.LevyMinStep * math.pow(u, -1f / alpha), r.LevyMinStep, r.LevyMaxStep);
    }

    public int RecruitNearby(float2 target, int count, float radius)
    {
        if (count <= 0 || Perception == null) return 0;
        double until = SimulationClock.TimeD + SimulationRules.Frame.RecruitWindow;
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, Neighbors);
        int n = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (n >= count || e == null || e.Colony != Colony || e.IsDisposed || e.IsDeathPending) continue;
            if (e.IsJuvenile || e.IsHungry || e.IsRecruited || e.IsCommandRunning || e.Plan.IsActive) continue;
            e.Recruit(target, until);
            n++;
        }
        return n;
    }

    public EntityModel FindHungriestKin(float radius)
    {
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, Neighbors);
        EntityModel best = null;
        float bestEnergy = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e == null || e.Colony != Colony || e.IsDisposed || e.IsDeathPending) continue;
            float en = e.EffectiveEnergyNorm;
            if (en >= bestEnergy) continue;
            bestEnergy = en;
            best = e;
        }
        return best;
    }

    public EntityModel FindSickestKin(float radius, float minLoad)
    {
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, Neighbors);
        EntityModel best = null;
        float bestLoad = minLoad;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e == null || e.Colony != Colony || e.IsDisposed || e.IsDeathPending || e.Virology == null) continue;
            e.Virology.TryGetViralInputs(out float load, out _, out _);
            if (load < bestLoad) continue;
            bestLoad = load;
            best = e;
        }
        return best;
    }

    private float2 _wildHome;
    private float2 _wildGoal;
    private bool   _wildHomeSet;
    private bool   _wildHasGoal;
    private bool   _wildLoiter;

    public void TickWild()
    {
        if (!_cycleActive) return;
        ref readonly var r = ref SimulationRules.Frame;

        float2 self = Position2D;
        if (!_wildHomeSet)
        {
            _wildHome    = self;
            _wildHomeSet = true;
        }

        float hp     = Stats.Hp / Stats.MaxHealth;
        bool hungry  = EffectiveEnergyNorm < r.PredatorHungerOn;
        bool flee    = hp < r.PredatorFleeHp && _threatResolved;
        bool meat    = _foodValid && _foodKind == FoodKind.Meat;
        var  prey    = hungry ? CachedHuntTarget : default;
        bool urgent  = flee || (hungry && (meat || prey.IsValid));

        if (_runner.IsRunning)
        {
            int current = _runner.CurrentAction;
            bool calm = current == (int)EntityAction.Wander || current == (int)EntityAction.Idle
                     || current == (int)EntityAction.Rest;
            if (!calm || !urgent) return;
        }

        if (flee)                                        StartWild(EntityAction.Flee, ActionDurationTable.Max((int)EntityAction.Flee));
        else if (hungry && meat && _foodDistance <= r.EatRange && !IsSated) StartWild(EntityAction.Eat, ActionDurationTable.Max((int)EntityAction.Eat));
        else if (hungry && meat)                         StartWild(EntityAction.ApproachFood, ActionDurationTable.Max((int)EntityAction.ApproachFood));
        else if (prey.IsValid)
        {
            _wildHome    = prey.Position2D;
            _wildHasGoal = false;
            StartWild(EntityAction.Attack, ActionDurationTable.Max((int)EntityAction.Attack));
        }
        else if (!hungry && hp < 1f)                     StartWild(EntityAction.Rest, r.PredatorRestSeconds);
        else if (_wildLoiter)
        {
            _wildLoiter = false;
            StartWild(EntityAction.Idle, RavineRandom.RangeFloat(r.PredatorLoiterMin, math.max(r.PredatorLoiterMin, r.PredatorLoiterMax)));
        }
        else Roam(in self, in r);
    }

    private void Roam(in float2 self, in SimulationRules.RulesFrame r)
    {
        if (_wildHasGoal && math.distance(self, _wildGoal) <= r.PlanArriveRadius)
        {
            _wildHasGoal = false;
            _wildLoiter  = true;
            StartWild(EntityAction.Idle, RavineRandom.RangeFloat(r.PredatorLoiterMin, math.max(r.PredatorLoiterMin, r.PredatorLoiterMax)));
            return;
        }

        if (!_wildHasGoal)
        {
            float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
            math.sincos(a, out float sa, out float ca);
            float2 goal = self + new float2(ca, sa) * r.PredatorRoamDistance;
            float2 fromHome = goal - _wildHome;
            if (math.lengthsq(fromHome) > r.PredatorHomeRange * r.PredatorHomeRange)
                goal = _wildHome + math.normalizesafe(fromHome) * math.max(r.PredatorHomeRange - r.PredatorRoamDistance, 0f);
            _wildGoal    = goal;
            _wildHasGoal = true;
        }

        float dist = math.distance(self, _wildGoal);
        float time = dist / math.max(Tuning.MoveSpeed * SpeedMul, 0.1f) * r.LegTimeSlack;
        var decision = new BrainDecision((int)EntityAction.Wander, 0, 0, SharedHierarchicalBrain.Goal.Hunt, _cycleNow,
            time, float2.zero, destination: _wildGoal, hasDestination: true);
        if (!TryStartCommand((int)EntityAction.Wander, in decision, CommandSource.Instinct)) _wildHasGoal = false;
    }

    private void StartWild(EntityAction action, float duration)
    {
        var decision = new BrainDecision((int)action, 0, 0, SharedHierarchicalBrain.Goal.Hunt, _cycleNow,
            duration, float2.zero);
        if (TryStartCommand((int)action, in decision, CommandSource.Instinct)) return;
        Roam(Position2D, in SimulationRules.Frame);
    }

    public float Drive()
    {
        ref readonly var r = ref SimulationRules.Frame;
        float stored = Digestion != null ? Digestion.Stomach : 0f;
        float en = math.saturate((Stats.En + stored * r.DigestEffActive) / Stats.MaxEnergy);
        float hp = math.saturate(Stats.Hp / Stats.MaxHealth);
        float de = 1f - en, dh = 1f - hp;
        return math.sqrt(r.DriveEnergyWeight * de * de + r.DriveHealthWeight * dh * dh);
    }

    public void BeginHomeostasis()
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
        amount *= _casteMods.DamageTaken;
        if (source != null) Points?.MarkNegative(Position2D, r.NegativePoiHit);
        Mortality?.NoteHarm(cause, Stats.Hp - amount);
        Stats.Hp -= amount;
        if (source != null && !ReferenceEquals(source, this))
        {
            _lastAttacker   = source;
            _lastAttackTime = SimulationClock.TimeD;
            _threatSource   = source.Position2D;
            _threatResolved = true;
        }

        var nest = Nest;
        if (nest == null) return;
        float norm = amount / Stats.MaxHealth;
        nest.Mark(ColonyChannel.Danger, Position2D, norm * r.NestDangerMark);
        if (IsAtNest) nest.RaiseAlarm(norm * r.AlarmFromDamage);

        double now = SimulationClock.TimeD;
        if (now - _lastAlarmCry < r.AlarmCryCooldown || IsDisposed || Stats.Hp <= 0f) return;
        _lastAlarmCry = now;
        Broadcast(r.AlarmCrySpeech);
        nest.RaiseAlarm(r.AlarmCryAmount);
    }

    public void Broadcast(float4 speech)
    {
        Speech.SetOwn(speech);
        if (Perception == null) return;

        ref readonly var r = ref SimulationRules.Frame;
        bool warn = _threatResolved && HasDirectThreat;
        float2 source = _threatSource;
        double until = SimulationClock.TimeD + r.WarnWindow;

        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, r.SpeechRadius, Neighbors);
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;
            e.Speech.ReceiveVector(speech);
            if (warn && e.Colony == Colony) e.Warn(until, source);
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

    public void InterruptCommand() => _runner?.Interrupt();

    public override void DeepClean()
    {
        _runner?.Abandon();
        Planner?.Cancel(this);
        _nearest = null;
        _nearestForeign = null;
        _lastAttacker = null;
        _playerTransform = null;
        Vectorizer?.Dispose();
        Vectorizer = null;
        _vecMaxHealth?.Dispose();
        _vecMaxEnergy?.Dispose();
        _vecMaxHealth = null;
        _vecMaxEnergy = null;
    }
}
