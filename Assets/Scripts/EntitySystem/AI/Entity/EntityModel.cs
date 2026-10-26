using UnityEngine;
using TheRavine.EntityControl;
using System;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public partial class EntityModel : IEntity, IFoodReceiver
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
    public VisualCullingComponent VisualCulling { get; set; }

    private readonly R3.ReactiveProperty<bool> _isActive = new(true);
    private readonly R3.ReactiveCommand<R3.Unit> _onUpdate = new();
    public R3.ReadOnlyReactiveProperty<bool> IsActive => _isActive;
    public R3.Observable<R3.Unit> OnUpdate => _onUpdate;
    public bool IsDisposed { get; private set; }
    public void Activate() => _isActive.Value = true;
    public void Deactivate() => _isActive.Value = false;

    public IEntityDialogHost DialogHost { get; private set; }

    public IEntityMotor Motor { get; private set; }
    public EntityTuning Tuning { get; private set; }
    public GameObject SelfObject { get; private set; }

    public ColonyState Colony { get; private set; }
    public int ColonyIndex => Colony != null ? Colony.Index : 0;
    public int ColonyId    => Colony != null ? Colony.ColonyId : 0;
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
        Vectorizer?.SetMaxima(tuning.MaxHealth, tuning.MaxEnergy);
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
    private readonly FitnessComponent _fitness = new();
    public FitnessComponent Fitness => _fitness;
    public int   FoodEaten      => _fitness.FoodEaten;
    public int   ReproduceCount => _fitness.ReproduceCount;
    public float DamageDealt    => _fitness.DamageDealt;
    public float FinalFitness   => _fitness.FinalFitness;

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
            return math.max(mul, r.MinSpeedMul);
        }
    }

    public float AttackDamageMul
        => 1f + (Virology != null ? Virology.Modifiers.Frenzy : 0f) * SimulationRules.Frame.FrenzyDamageMul;

    public float BodyEnergy => Stats.En + (Digestion != null ? Digestion.Stomach : 0f) + Carrying;

    public float EffectiveEnergyNorm
        => (Stats.En + (Digestion != null ? Digestion.Stomach : 0f) * SimulationRules.Frame.DigestEffActive) / Stats.MaxEnergy;

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

    public void SetUp()
    {
        _lastCycleTime   = SimulationClock.TimeD;
        _attackReadyTime = 0d;
        BeginHomeostasis();
    }

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

        Stats = new StatsComponent();
        Stats.FillComponent(tuning.MaxHealth, tuning.MaxEnergy);

        Digestion = new DigestionComponent();
        Digestion.Configure(tuning.MaxEnergy * SimulationRules.Active.StomachCapacityFraction);

        ServiceLocator.Services.TryGet(out _grid);
        Perception = new PerceptionComponent(tuning.DetectionRadius, _grid);

        _terrain = new TerrainSensor(ServiceLocator.GetService<MapGenerator>());
        ServiceLocator.Services.TryGet(out _foodIndex);

        ServiceLocator.Services.TryGet(out MovePlanner planner);
        ServiceLocator.Services.TryGet(out _players);
        ServiceLocator.Services.TryGet(out _thresholds);
        ServiceLocator.Services.TryGet(out InfectionService infection);
        Nest      = colony.Nest;
        Planner   = planner;
        Infection = infection;

        Speech = new SpeechComponent();
        Speech.Inject((IEntityAudio)motor);
        Points = new PointsOfInterestComponent();

        Brain = new BrainComponent(brain, ctx);

        _runner = new EntityCommandRunner(this);
        _plan   = new PlanRunner(this);

        Mortality = new MortalityComponent(Stats);
        Mortality.Died += () =>
        {
            _plan.Interrupt();
            InterruptCommand();
        };
        Mortality.Died += () => death?.OnDeath();

        float startHealth = colony.IsWild ? tuning.MaxHealth : tuning.MaxHealth * rulesA.JuvenileHealthMul;
        if (!colony.IsWild) Stats.Rescale(startHealth, tuning.MaxEnergy);

        Vectorizer = new InputVectorizer(startHealth, tuning.MaxEnergy);

        Virology = new VirologyComponent();
        uint seed = (uint)RavineRandom.RangeInt(1, int.MaxValue);
        var parentVirology = parent?.Virology;
        if (parentVirology != null && parentVirology.IsCreated && !parentVirology.IsDisposed)
            Virology.FillFromParent(parentVirology, in ctx.CoordMLP.Params, seed);
        else
            Virology.FillComponent(in ctx.CoordMLP.Params, seed);
        Virology.SetImmuneStrength(math.lerp(rulesA.ImmuneThresholdMin, rulesA.ImmuneThresholdMax, ctx.CoordMLP.Params.ImmuneInvestment));
    }

    public void Init()
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

    private static readonly Unity.Profiling.ProfilerMarker PerceptionMarker = new("EntityModel.Perception");

    private bool  _cycleActive;
    private float _cycleNow;
    private float _cycleDt;

    public bool CycleActive => _cycleActive;

    private static readonly EntityModel[] ForeignScratch = new EntityModel[16];
    private static ColonyRegistry _registry;

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

    private float2 _wildHome;
    private float2 _wildGoal;
    private bool   _wildHomeSet;
    private bool   _wildHasGoal;
    private bool   _wildLoiter;

    public void InterruptCommand() => _runner?.Interrupt();

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        DeepClean();

        Stats?.Dispose();
        Digestion?.Dispose();
        _fitness.Dispose();
        Perception?.Dispose();
        Speech?.Dispose();
        Points?.Dispose();
        Brain?.Dispose();
        Mortality?.Dispose();
        Virology?.Dispose();
        VisualCulling?.Dispose();

        _isActive.Dispose();
        _onUpdate.Dispose();
    }

    private void DeepClean()
    {
        _runner?.Abandon();
        Planner?.Cancel(this);
        _nearest = null;
        _nearestForeign = null;
        _lastAttacker = null;
        _playerTransform = null;
        Vectorizer?.Dispose();
        Vectorizer = null;
    }
}
