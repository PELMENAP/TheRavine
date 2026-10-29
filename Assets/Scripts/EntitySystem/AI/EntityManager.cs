using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

using Unity.Mathematics;
using Unity.Profiling;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.EntityControl.Virology;

public class EntityManager : MonoBehaviour
{
    
    [Header("Prefabs")]
    [SerializeField] private GameObject entityPrefab;
    [SerializeField] private EntityTuning tuning;

    [Header("Population")]
    [SerializeField] private int   initialCount  = 20;
    [SerializeField] private int   maxPopulation = 200;
    [SerializeField] private float spawnRadius   = 100f;

    [Header("Colonies")]
    [SerializeField] private Transform[] colonyOrigins;

    [Header("Food")]
    [SerializeField] private int maxFood = 100;

    [Header("Brain")]
    [SerializeField] private int lstmHidden  = 32;
    [SerializeField] private string savedModelName = "shared_brain";

    [Header("Diagnostics")]
    [SerializeField] private int  _entityCount;
    [SerializeField] private int  _foodCount;
    [SerializeField] private float _avgEntropy;

    [Header("Rules")]
    [SerializeField] private SimulationRules rules;

    [Header("Visual culling")]
    [SerializeField] private bool cullOutsideActiveChunks = true;
    [SerializeField] private int  cullChunkMargin = 0;
    private MapGenerator _map;

    [Header("Nest views")]
    [SerializeField] private bool showNestViews = true;
    [SerializeField] private NestDebugView nestViewPrefab;
    private readonly List<NestDebugView> _nestViews = new();

    private InfectionCoordinator _infection;
    private ColonyFood           _food;
    private EvolutionService     _evolution;
    private ColonyPopulation     _population;

    [SerializeField] private int _transmissions;
    [SerializeField] private int _recombinations;
    [SerializeField] private int _superinfectionBlocks;
    [SerializeField] private float _avgFitness;

    [SerializeField] private bool useSavedBrain;

    private float   _nextGenerationTime;

    private readonly EntitySpatialGrid _grid = new();
    public EntitySpatialGrid Grid => _grid;

    public int MaxPopulation => maxPopulation;
    public event Action<EntityModel> OnEntitySpawned;
    public event Action<EntityModel> OnEntityDied;

    private void RaiseSpawned(EntityModel model) => OnEntitySpawned?.Invoke(model);
    private void RaiseDied(EntityModel model)    => OnEntityDied?.Invoke(model);

    private ColonyRegistry _colonies;
    public ColonyRegistry Colonies => _colonies;

    private InstinctSystem _instincts;
    private BoidSystem _boids;
    private readonly PlayerPresence _players = new();
    public PlayerPresence Players => _players;
    private UtilityPriorSystem   _utility;
    private CasteThresholdSystem _thresholds;
    private ColonyState _wild;
    private float  _lastColonyTime;
    public InstinctSystem Instincts => _instincts;

    private readonly List<EntityModel> _entities = new();
    public List<EntityModel> Entities => _entities;

    public ChunkFoodIndex FoodIndex => _food != null ? _food.Index : null;

    [SerializeField] private float simulationTimeScale = 1f;

    private EntityModel[] _tickSnapshot = new EntityModel[64];
    private int _tickCount;
    private int _tickCursor;
    private bool _windowOpen;
    private bool _simulationStarted;
    private double _tickStartTime;
    private double _nextFitnessStatsTime;
    private TickWindowBudget _tickBudget;

    private static readonly ProfilerMarker TickAgentsMarker  = new("EntityManager.TickAgents");
    private static readonly ProfilerMarker SnapshotMarker    = new("EntityManager.RebuildTickSnapshot");
    private static readonly ProfilerMarker BatchMarker       = new("EntityManager.RunTickBatch");
    private static readonly ProfilerMarker CloseWindowMarker = new("EntityManager.CloseTickWindow");

    public EntityModel GetRandomEntity()
        => _entities.Count == 0 ? null : _entities[RavineRandom.RangeInt(0, _entities.Count)];

    public bool SeedProbeStrain(EntityModel target, ProteinAction[] recipe)
        => _infection != null && _infection.SeedRecipe(target, recipe);

    public EntityModel SpawnEntity(Vector3 position, ColonyState colony, EntityBrainContext inheritedCtx = null, EntityModel parent = null)
        => _population.SpawnEntity(position, colony, inheritedCtx, parent);

    public void SpawnChild(EntityModel parent) => _population.SpawnChild(parent);

    private void UpdateDiagnostics()
    {
        _evolution.UpdateFitnessStats();
        _entityCount = _entities.Count;
        _foodCount   = _food.FoodCount;
        _avgEntropy  = _evolution.AvgEntropy;
        _avgFitness  = _evolution.AvgFitness;
    }

    public void EvolveGenotypes()
    {
        if (_tickCursor != 0) return;
        _evolution.Evolve();
    }

    [ContextMenu("Seed Random Strain")]
    private void SeedRandomStrain() => _infection?.SeedRandomStrain(_entities, 12);

    private void Update()
    {
        SimulationClock.Advance(Time.deltaTime * simulationTimeScale);

        if (_simulationStarted)
        {
            double now = SimulationClock.TimeD;
            if (now >= _nextFitnessStatsTime)
            {
                UpdateDiagnostics();
                _nextFitnessStatsTime = now + SimulationRules.Active.FitnessStatsInterval;
            }
            if (now >= _tickStartTime) TickAgents(SimulationClock.DeltaTime);
        }

        _planner.Flush();
        _motion.Step();
    }

    private MovePlanner _planner;
    private float _lastNestTime;

    public void ReportThreat(ColonyState colony, Vector3 position, float magnitude)
    {
        if (colony == null) return;
        var p = new Unity.Mathematics.float2(position.x, position.z);
        colony.Nest.Mark(ColonyChannel.Danger, p, magnitude);
        colony.Nest.RaiseAlarm(magnitude);
    }
    private MotionSystem _motion;
    public MotionSystem Motion => _motion;

    private void Awake()
    {
        SimulationRules.ReleaseVariant();
        SimulationRules.Bind(ResolveRules());
        ContextSlabs.Reserve(maxPopulation);
        _motion = new MotionSystem(maxPopulation);
        _instincts = new InstinctSystem(maxPopulation);
        _boids     = new BoidSystem(maxPopulation);
        _utility    = new UtilityPriorSystem(maxPopulation);
        _thresholds = new CasteThresholdSystem(maxPopulation);
        ServiceLocator.Register(_thresholds);
        ServiceLocator.Register(_grid);
        ServiceLocator.Register(_players);
        _colonies = new ColonyRegistry();
        CreateColonies();
        ServiceLocator.Register(_colonies);
        _planner = new MovePlanner(_motion);
        _planner.BindColonies(_colonies);
        ServiceLocator.Register(_planner);
        NeuralModelStorage.RegisterFactory(new SharedBrainSnapshotFactory());
        _infection = new InfectionCoordinator((uint)UnityEngine.Random.Range(1, int.MaxValue));
        ServiceLocator.Register(_infection.Service);
        _food       = new ColonyFood(maxFood);
        _evolution  = new EvolutionService(_entities, _colonies, (uint)UnityEngine.Random.Range(1, int.MaxValue));
        _population = new ColonyPopulation(entityPrefab, transform, tuning, maxPopulation, spawnRadius,
            _entities, _colonies, _wild, _food, _evolution, _grid, _motion, _instincts, _utility, _thresholds, _boids);
        _population.Spawned += RaiseSpawned;
        _population.Died    += RaiseDied;

        if (useSavedBrain)
            LoadBrain();
    }

    private void CreateColonies()
    {
        int n = colonyOrigins != null ? colonyOrigins.Length : 0;
        for (int i = 0; i < n; i++)
            if (colonyOrigins[i] != null) AddColony(colonyOrigins[i].position);

        if (_colonies.Count == 0) AddColony(transform.position);
        if (SimulationRules.Active.PredatorCount > 0) _wild = AddColony(transform.position, true);
    }

    private ColonyState AddColony(Vector3 position, bool wild = false)
        => _colonies.Add(new Unity.Mathematics.float2(position.x, position.z),
            new SharedHierarchicalBrain(InputVectorizer.VectorSize, lstmHidden), maxPopulation, wild);



    [ContextMenu("Save Brain")]
    private void SaveBrain() => SaveBestBrainAsync().Forget();

    [ContextMenu("Load Brain")]
    private void LoadBrain() => LoadBrainAsync().Forget();

    private string ColonyModelName(ColonyState colony) => NeuralModelStorage.ColonyFileName(savedModelName, colony.ColonyId);

    private async UniTaskVoid SaveBestBrainAsync()
    {
        for (int c = 0; c < _colonies.Count; c++)
        {
            var colony = _colonies[c];
            if (colony.Brain == null) continue;
            await NeuralModelStorage.SaveAsync(colony.Brain.ToSnapshot(), ColonyModelName(colony), destroyCancellationToken);
        }
    }

    private async UniTaskVoid LoadBrainAsync()
    {
        SharedHierarchicalBrain fallback = null;

        for (int c = 0; c < _colonies.Count; c++)
        {
            var colony   = _colonies[c];
            string file  = ColonyModelName(colony);
            var snapshot = await NeuralModelStorage.LoadAsync<SharedBrainSnapshot>(file, destroyCancellationToken);
            var brain    = snapshot != null
                ? SharedHierarchicalBrain.FromSnapshot(snapshot, InputVectorizer.VectorSize, lstmHidden)
                : null;

            if (brain == null)
            {
                if (fallback == null)
                {
                    var shared = await NeuralModelStorage.LoadAsync<SharedBrainSnapshot>(savedModelName, destroyCancellationToken);
                    fallback = shared != null
                        ? SharedHierarchicalBrain.FromSnapshot(shared, InputVectorizer.VectorSize, lstmHidden)
                        : null;
                    if (fallback == null)
                    {
                        RavineLog.Warning($"Колония {colony.ColonyId}: снапшот {file} не загружен, используется новый мозг");
                        continue;
                    }
                    brain = fallback;
                }
                else brain = new SharedHierarchicalBrain(fallback);
                RavineLog.Warning($"Колония {colony.ColonyId}: снапшот {file} не загружен, взят общий {savedModelName}");
            }

            if (!ReferenceEquals(brain, colony.Brain)) ApplyLoadedBrain(colony, brain);
        }
    }

    private void ApplyLoadedBrain(ColonyState colony, SharedHierarchicalBrain brain)
    {
        var old = colony.Brain;
        colony.ReplaceBrain(brain);

        var members = colony.Members;
        for (int i = 0; i < members.Length; i++)
        {
            var e = _entities[members[i]];
            if (!e.IsDisposed) e.Brain.ReplaceBrain(brain);
        }

        old?.Dispose();
    }

    
    private async void Start()
    {
        if (await DelaySimulation(SimulationRules.Active.StartupDelaySeconds, destroyCancellationToken)) return;

        var map = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
        _motion.Inject(map);
        _map = map;

        var foodIndex = await ServiceLocator.WaitUntilServiceReady<ChunkFoodIndex>();
        _food.Bind(foodIndex, SimulationClock.Time);

        for (int i = 0; i < initialCount; i++)
        {
            var colony = _colonies[i % _population.CivilCount];
            SpawnEntity(_population.RandomPosition(colony), colony);
        }
        _population.StartPredatorClock(SimulationClock.TimeD);
        _lastColonyTime   = SimulationClock.Time;
        CreateNestViews();

        _nextGenerationTime = SimulationClock.Time + SimulationRules.Active.GenerationInterval;
        _lastNestTime       = SimulationClock.Time;

        _nextFitnessStatsTime = SimulationClock.TimeD;
        _tickStartTime        = SimulationClock.TimeD + SimulationRules.Active.TickLoopWarmupSeconds;
        _simulationStarted    = true;
    }


    private static async UniTask<bool> DelaySimulation(float seconds, CancellationToken ct)
    {
        double until = SimulationClock.TimeD + seconds;
        do
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return true;
        }
        while (SimulationClock.TimeD < until);
        return false;
    }

    private void TickAgents(float dt)
    {
        using var scope = TickAgentsMarker.Auto();
        if (!_windowOpen && _entities.Count == 0)
        {
            _tickBudget.Reset();
            _population.ProcessPendingDeaths();
            BeginColonyBatches();
            RunColonyDecisions();
            _population.FlushDeferredDisposals();
            _food.Tick();
            return;
        }

        int count = _windowOpen ? _tickCount : _entities.Count;
        int batch = _tickBudget.Take(dt, SimulationRules.Active.AgentTickInterval, count, count - _tickCursor);

        if (batch > 0)
        {
            if (!_windowOpen)
            {
                RebuildTickSnapshot();
                _windowOpen = true;
            }

            int end = _tickCursor + batch;
            if (end > _tickCount) end = _tickCount;

            RunTickBatch(_tickCursor, end);

            _planner.Flush();
            _infection.Spread(_tickSnapshot, _tickCursor, end);

            _transmissions        = _infection.Transmissions;
            _recombinations       = _infection.Recombinations;
            _superinfectionBlocks = _infection.SuperinfectionBlocks;

            _tickCursor = end;
            if (_tickCursor >= _tickCount) CloseTickWindow();
        }

        _food.Tick();
    }

    private void CloseTickWindow()
    {
        using var scope = CloseWindowMarker.Auto();
        _tickCursor = 0;
        _windowOpen = false;
        _population.ProcessPendingDeaths();
        _population.CheckExtinctions();
        _population.TickPredators();
        for (int c = 0; c < _colonies.Count; c++) _colonies[c].Brain.ApplyPendingGradients();
        _food.PruneUnloaded();
        TickNests();
        TickColonies();
        UpdateVisibility();

        float now = SimulationClock.Time;
        if (now >= _nextGenerationTime)
        {
            if (SimulationRules.Active.PeriodicGenotypeOverwrite) EvolveGenotypes();
            _nextGenerationTime = now + SimulationRules.Active.GenerationInterval;
        }
    }

    private void RunTickBatch(int start, int end)
    {
        using var scope = BatchMarker.Auto();
        BeginColonyBatches();
        _players.Refresh();

        int lo = int.MaxValue, hi = -1;
        for (int i = start; i < end; i++)
        {
            var e = _tickSnapshot[i];
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;
            if (!e.BeginCycle()) continue;

            int idx = e.ManagerIndex;
            _instincts.Write(idx, in e.InstinctInput);
            _utility.Write(idx, in e.UtilityInput);
            if (idx < lo) lo = idx;
            if (idx > hi) hi = idx;
        }

        if (hi >= lo)
        {
            _instincts.Evaluate(lo, hi + 1);
            _utility.Evaluate(lo, hi + 1);
        }

        for (int i = start; i < end; i++)
        {
            var e = _tickSnapshot[i];
            if (e == null || e.IsDisposed || !e.CycleActive) continue;
            if (e.IsWild)
            {
                e.TickWild();
                continue;
            }
            e.ApplyUtilityPrior(_utility.Output(e.ManagerIndex));
            var output = _instincts.Output(e.ManagerIndex);
            e.ApplyInstinct(in output);
            e.SubmitDecision(output.NeedsDecision != 0 && !e.IsCommandRunning && !e.Plan.IsActive);
        }

        RunColonyDecisions();
        _population.FlushDeferredDisposals();

        for (int i = start; i < end; i++)
        {
            var e = _tickSnapshot[i];
            if (e == null || e.IsDisposed) continue;
            e.EndCycle();
        }
    }

    private void BeginColonyBatches()
    {
        for (int c = 0; c < _colonies.Count; c++) _colonies[c].Brain.BeginDecisionBatch();
    }

    private void RunColonyDecisions()
    {
        for (int c = 0; c < _colonies.Count; c++) _colonies[c].Brain.RunDecisions();
    }

    private void RunFlocking()
    {
        int n = _entities.Count;
        for (int i = 0; i < n; i++)
        {
            var agent = _entities[i].BuildBoidAgent();
            _boids.Write(i, in agent);
        }

        _boids.Run(n);

        for (int i = 0; i < n; i++)
        {
            var output = _boids.Output(i);
            _entities[i].SetFlock(in output);
        }
    }

    private void TickColonies()
    {
        var r = SimulationRules.Active;
        double now = SimulationClock.TimeD;
        float  dt  = SimulationClock.Time - _lastColonyTime;
        _lastColonyTime = SimulationClock.Time;

        for (int c = 0; c < _colonies.Count; c++)
        {
            var colony  = _colonies[c];
            var members = colony.Members;
            colony.TickEma(dt);
            if (colony.IsWild) continue;

            Array.Clear(colony.CasteCounts, 0, colony.CasteCounts.Length);
            colony.JuvenileCount = 0;
            for (int i = 0; i < members.Length; i++)
            {
                var e = _entities[members[i]];
                if (e.IsJuvenile) colony.JuvenileCount++;
                else if (e.CasteFixed) colony.CasteCounts[(int)e.Caste]++;
            }

            for (int i = 0; i < members.Length; i++)
            {
                var e = _entities[members[i]];
                if (e.IsDisposed || e.IsDeathPending || e.IsJuvenile) continue;

                if (!e.CasteFixed)
                {
                    var caste = ChooseCaste(colony, members.Length, e, false);
                    e.AssignCaste(caste);
                    e.NextCasteReview = now + r.CasteReviewInterval;
                    colony.CasteCounts[(int)caste]++;
                    continue;
                }

                if (now >= e.NextCasteReview)
                {
                    e.NextCasteReview = now + r.CasteReviewInterval;
                    colony.CasteCounts[(int)e.Caste]--;
                    var review = ChooseCaste(colony, members.Length, e, true);
                    if (review != e.Caste) e.AssignCaste(review);
                    colony.CasteCounts[(int)review]++;
                }

                var virology = e.Virology;
                if (virology == null || virology.Modifiers.CasteShift < r.CasteShiftThreshold) continue;
                if (now - e.LastCasteShift < r.CasteShiftCooldown) continue;

                e.LastCasteShift = now;
                colony.CasteCounts[(int)e.Caste]--;
                var shifted = (Caste)(((int)e.Caste + 1) % (int)Caste.Count);
                e.AssignCaste(shifted);
                colony.CasteCounts[(int)shifted]++;
            }

            var leader = colony.Leader;
            bool vacant = leader == null || leader.IsDisposed || leader.IsDeathPending;
            if (!vacant && now < colony.NextElection) continue;

            colony.NextElection = now + r.LeaderElectionInterval;
            colony.SetLeader(_population.ElectLeader(colony));
        }
    }

    private readonly float[] _casteWeights = new float[(int)Caste.Count];

    private Caste ChooseCaste(ColonyState colony, int members, EntityModel e, bool review)
    {
        var r    = SimulationRules.Active;
        var nest = colony.Nest;
        float inv = 1f / math.max(members - colony.JuvenileCount, 1);
        float4 th = _thresholds.Get(e.ManagerIndex);

        float age   = e.AgeFraction;
        float poly  = r.PolyethismStimulus;
        float young = age < r.NurseAgeEnd ? poly : 0f;
        float mid   = age >= r.NurseAgeEnd && age < r.WorkerAgeEnd ? poly : 0f;
        float old   = age >= r.WorkerAgeEnd ? poly : 0f;
        float winter = SeasonClock.WinterApproach(SimulationClock.Time) * r.PreWinterWorkerStimulus;

        float storage = math.saturate(nest.Storage / math.max(r.NestStorageNorm, 1e-3f));
        Stimulus(Caste.Soldier, math.saturate(nest.Alarm + nest.DangerAtNest) + old, th[(int)Caste.Soldier], colony, inv);
        Stimulus(Caste.Worker,  1f - storage + mid + winter,                   th[(int)Caste.Worker],  colony, inv);
        Stimulus(Caste.Scout,   math.saturate(nest.Hunger + (nest.HasFoodPeak ? 0f : r.CasteScoutNoFoodStimulus)) + old, th[(int)Caste.Scout], colony, inv);
        Stimulus(Caste.Nurse,   math.saturate(colony.JuvenileCount * r.CasteNurseGain / math.max(members, 1)) + young, th[(int)Caste.Nurse], colony, inv);

        float total = 0f, top = 0f;
        for (int c = 0; c < _casteWeights.Length; c++)
        {
            total += _casteWeights[c];
            top = math.max(top, _casteWeights[c]);
        }
        if (total <= 1e-6f) return review ? e.Caste : Caste.Worker;
        if (review && _casteWeights[(int)e.Caste] >= top * r.CasteSwitchMargin) return e.Caste;

        float pick = RavineRandom.RangeFloat(0f, total);
        for (int c = 0; c < _casteWeights.Length; c++)
        {
            pick -= _casteWeights[c];
            if (pick <= 0f) return (Caste)c;
        }
        return Caste.Worker;
    }

    private void Stimulus(Caste caste, float need, float threshold, ColonyState colony, float invAdults)
    {
        float s  = math.max(0f, need - colony.CasteCounts[(int)caste] * invAdults);
        float s2 = s * s;
        _casteWeights[(int)caste] = s2 / (s2 + threshold * threshold + 1e-6f);
    }

    private void TickNests()
    {
        float now = SimulationClock.Time;
        float dt  = now - _lastNestTime;
        _lastNestTime = now;

        var r = SimulationRules.Active;
        for (int c = 0; c < _colonies.Count; c++)
        {
            var colony  = _colonies[c];
            var members = colony.Members;
            int atNest  = 0, restingAtNest = 0, hungryAtNest = 0, nursesAtNest = 0, far = 0;
            float fill  = 0f;
            float far2  = r.NestRadius * r.NestFarRadiusMul;
            far2 *= far2;
            float2 nestPos = colony.Nest.Position;
            for (int i = 0; i < members.Length; i++)
            {
                var e = _entities[members[i]];
                if (e.IsDisposed) continue;
                fill += e.Digestion.Fill;
                if (math.distancesq(e.Position2D, nestPos) > far2) far++;
                if (!e.IsAtNest) continue;
                atNest++;
                if (e.IsHungry) hungryAtNest++;
                if (e.Caste == Caste.Nurse && e.CasteFixed && !e.IsJuvenile) nursesAtNest++;
                if (e.Plan.Kind == PlanKind.Rest || (e.IsCommandRunning && e.LastAction == EntityAction.Rest)) restingAtNest++;
            }
            colony.Nest.HungryAtNest = hungryAtNest;
            colony.Nest.NursesAtNest = nursesAtNest;

            colony.Nest.Tick(dt, atNest);

            float inv = members.Length > 0 ? 1f / members.Length : 0f;
            colony.Stats.SampleMembers(fill * inv, restingAtNest * inv,
                math.saturate(colony.Nest.Storage / math.max(r.NestStorageNorm, 1e-3f)));
            colony.Stats.FarShare = far * inv;
        }
    }

    public void SetEntityVisible(EntityModel model, bool visible)
    {
        if (model == null || model.IsDisposed) return;
        model.VisualCulling?.SetVisible(visible);
    }

    public void SetEntitiesVisible(IReadOnlyList<EntityModel> targets, bool visible)
    {
        for (int i = 0; i < targets.Count; i++)
            SetEntityVisible(targets[i], visible);
    }

    private void RebuildTickSnapshot()
    {
        using var scope = SnapshotMarker.Auto();
        int count = _entities.Count;

        if (count > _tickSnapshot.Length)
        {
            int cap = _tickSnapshot.Length;
            while (cap < count) cap <<= 1;
            _tickSnapshot = new EntityModel[cap];
        }

        for (int i = 0; i < count; i++)
            _tickSnapshot[i] = _entities[i];

        if (_tickCount > count)
            Array.Clear(_tickSnapshot, count, _tickCount - count);

        _tickCount = count;

        SimulationRules.CaptureFrame();
        _grid.Rebuild(_tickSnapshot, _tickCount, tuning.DetectionRadius);
        RunFlocking();
    }

    private void UpdateVisibility()
    {
        if (!cullOutsideActiveChunks || _map == null) return;
        bool hasViewer = _map.TryGetViewerChunk(out int vx, out int vz);
        int range = MapGenerator.chunkScale + cullChunkMargin;

        for (int i = 0; i < _entities.Count; i++)
        {
            var e = _entities[i];
            if (e.IsDisposed || e.IsDeathPending) continue;
            bool visible = true;
            if (hasViewer)
            {
                float2 p = e.Position2D;
                visible = math.abs(MapGenerator.ChunkCoord(p.x) - vx) <= range
                       && math.abs(MapGenerator.ChunkCoord(p.y) - vz) <= range;
            }
            SetEntityVisible(e, visible);
        }
    }

    private void CreateNestViews()
    {
        if (!showNestViews) return;
        for (int c = 0; c < _colonies.Count; c++)
        {
            var view = nestViewPrefab != null ? Instantiate(nestViewPrefab) : NestDebugView.Create(null);
            view.Bind(_colonies[c], this);
            _nestViews.Add(view);
        }
    }

    private void UnregisterServices()
    {
        var services = ServiceLocator.Services;
        services.Unregister(_thresholds);
        services.Unregister(_grid);
        services.Unregister(_players);
        services.Unregister(_colonies);
        services.Unregister(_planner);
        services.Unregister(_infection?.Service);
    }

    private SimulationRules ResolveRules()
    {
        if (rules == null) return null;
        if (!ServiceLocator.Services.TryGet(out TheRavine.Base.WorldRegistry registry) || !registry.HasLoadedWorld) return rules;

        var state = registry.GetCurrentState();
        float[] multipliers = state.rulesMultipliers;
        if (multipliers == null || multipliers.Length != (int)VarianceAxis.Count)
        {
            multipliers = rules.RollVariance(state.seed);
            registry.UpdateState(s => s.rulesMultipliers = multipliers);
        }

        return rules.CreateVariant(multipliers);
    }

    private void OnDestroy()
    {
        SimulationRules.ReleaseVariant();
        UnregisterServices();
        for (int i = 0; i < _nestViews.Count; i++)
            if (_nestViews[i] != null) Destroy(_nestViews[i].gameObject);
        _nestViews.Clear();
        _infection?.Dispose();
        _population?.ProcessPendingDeaths();
        _population?.FlushDeferredDisposals();
        _planner?.Dispose();
        _motion?.Dispose();
        _instincts?.Dispose();
        _boids?.Dispose();
        _utility?.Dispose();
        _thresholds?.Dispose();
        _colonies?.Dispose();
        ContextSlabs.DisposeAll();
    }
}