using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

using TheRavine.Generator;
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

    [Header("Food")]
    [SerializeField] private int initialFood = 50;
    [SerializeField] private int maxFood     = 100;

    [Header("Brain")]
    [SerializeField] private int lstmHidden  = 32;
    [SerializeField] private string savedModelName = "shared_brain";

    [Header("Diagnostics")]
    [SerializeField] private int  _entityCount;
    [SerializeField] private int  _foodCount;
    [SerializeField] private float _avgEntropy;

    [Header("Rules")]
    [SerializeField] private SimulationRules rules;

    private InfectionService _infection;
    private uint _virologyTick;

    [SerializeField] private int _transmissions;
    [SerializeField] private int _recombinations;
    [SerializeField] private int _superinfectionBlocks;
    [SerializeField] private float _avgFitness;

    [Header("Virology")]
    [SerializeField] private StrainCodonTable strainCodonTable;

    [SerializeField] private bool useSavedBrain;

    private float[] _fitnessScratch = new float[64];
    private float   _fitnessMedian;
    private float   _fitnessSpread = 1f;
    private bool    _fitnessStatsReady;

    private int[]   _evolveIndices = new int[64];
    private float[] _evolveKeys    = new float[64];
    private float   _nextGenerationTime;

    private XorShift32 _tournamentRng;

    private readonly EntitySpatialGrid _grid = new();
    public EntitySpatialGrid Grid => _grid;

    public int MaxPopulation => maxPopulation;
    public event Action<EntityModel> OnEntitySpawned;
    public event Action<EntityModel> OnEntityDied;

    private SharedHierarchicalBrain _sharedBrain;
    public SharedHierarchicalBrain SharedBrain => _sharedBrain;
    private readonly List<EntityModel> _entities = new();
    public List<EntityModel> Entities => _entities;
    private readonly Queue<EntityModel> _pendingDeath = new();

    private const int FoodSpawnAttempts = 16;

    private ChunkFoodIndex _foodIndex;
    public ChunkFoodIndex FoodIndex => _foodIndex;

    [SerializeField] private float simulationTimeScale = 1f;

    private const float TickWindowSeconds = 1f;

    private EntityModel[] _tickSnapshot = new EntityModel[64];
    private int _tickCount;
    private int _tickCursor;
    private int _tickBatch = 1;

    public EntityModel GetRandomEntity()
        => _entities.Count == 0 ? null : _entities[RavineRandom.RangeInt(0, _entities.Count)];

    public bool SeedProbeStrain(EntityModel target, ProteinAction[] recipe)
            => _infection != null
            && _infection.InjectRecipe(target, recipe, (uint)RavineRandom.RangeInt(1, int.MaxValue));

    private void Update()
    {
        SimulationClock.Advance(Time.deltaTime * simulationTimeScale);
        _planner.Flush();
        _motion.Step();
    }

    private NestState _nest;
    public NestState Nest => _nest;
    private MovePlanner _planner;
    private float _lastEcologyTime;
    private float _lastNestTime;

    public void ReportThreat(Vector3 position, float magnitude)
    {
        if (_nest == null) return;
        var p = new Unity.Mathematics.float2(position.x, position.z);
        _nest.MarkDanger(p, magnitude);
        _nest.RaiseAlarm(magnitude);
    }
    private MotionSystem _motion;
    public MotionSystem Motion => _motion;

    private void Awake()
    {
        SimulationRules.Bind(rules);
        ContextSlabs.Reserve(maxPopulation);
        _motion = new MotionSystem(maxPopulation);
        ServiceLocator.Services.Register(_grid);
        Vector3 home = transform.position;
        _nest = new NestState(new Unity.Mathematics.float2(home.x, home.z));
        ServiceLocator.Services.Register(_nest);
        _planner = new MovePlanner(_motion);
        _planner.BindNest(_nest);
        ServiceLocator.Services.Register(_planner);
        NeuralModelStorage.RegisterFactory(new SharedBrainSnapshotFactory());
        _infection = new InfectionService((uint)UnityEngine.Random.Range(1, int.MaxValue));
        ServiceLocator.Services.Register(_infection);
        _tournamentRng = new XorShift32((uint)UnityEngine.Random.Range(1, int.MaxValue));
        
        if(useSavedBrain)
            LoadBrain();
        else
            _sharedBrain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, lstmHidden);
    }


    [ContextMenu("Save Brain")]
    private void SaveBrain() => SaveBestBrainAsync().Forget();

    [ContextMenu("Load Brain")]
    private void LoadBrain() => LoadBrainAsync().Forget();

    private async UniTaskVoid SaveBestBrainAsync()
    {
        if (_sharedBrain == null) return;
        await NeuralModelStorage.SaveAsync(_sharedBrain.ToSnapshot(), savedModelName, destroyCancellationToken);
    }

    private async UniTaskVoid LoadBrainAsync()
    {
        var snapshot = await NeuralModelStorage.LoadAsync<SharedBrainSnapshot>(savedModelName, destroyCancellationToken);
        if (snapshot == null) return;

        var brain = SharedHierarchicalBrain.FromSnapshot(snapshot, InputVectorizer.VectorSize, lstmHidden);
        if (brain == null || ReferenceEquals(brain, _sharedBrain)) return;

        var old = _sharedBrain;
        _sharedBrain = brain;
        for (int i = 0; i < _entities.Count; i++)
            if (!_entities[i].IsDisposed)
                _entities[i].Brain.ReplaceBrain(brain);

        old?.Dispose();
    }
    private CancellationTokenSource _tickCts;

    
    private async void Start()
    {
        await UniTask.Delay(3000);

        var map = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
        _motion.Inject(map);

        _foodIndex = new ChunkFoodIndex(map);
        ServiceLocator.Services.Register(_foodIndex);

        for (int i = 0; i < initialCount; i++)
            SpawnEntity(RandomPosition());

        for (int i = 0; i < initialFood; i++)
            SpawnFood();

        _nextGenerationTime = SimulationClock.Time + SimulationRules.Active.GenerationInterval;
        _lastEcologyTime    = SimulationClock.Time;
        _lastNestTime       = SimulationClock.Time;

        _tickCts = new CancellationTokenSource();
        EntityTickLoopAsync(_tickCts.Token).Forget();

        TrackDiagnosticsAsync(destroyCancellationToken).Forget();
    }


    private async UniTaskVoid EntityTickLoopAsync(CancellationToken ct)
    {
        await UniTask.Delay(3000, cancellationToken: ct);

        while (!ct.IsCancellationRequested)
        {
            if (_tickCursor == 0)
            {
                RebuildTickSnapshot();

                int frames = Mathf.Clamp(
                    Mathf.CeilToInt(TickWindowSeconds / Mathf.Max(Time.smoothDeltaTime, 0.001f)),
                    1, 240);

                _tickBatch = _tickCount > 0 ? (_tickCount + frames - 1) / frames : 1;
            }

            if (_tickCount == 0)
            {
                ProcessPendingDeaths();
                _sharedBrain.BeginDecisionBatch();
                _sharedBrain.RunDecisions();
                FlushDeferredDisposals();
                TickFoodRespawn();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                continue;
            }

            int end = _tickCursor + _tickBatch;
            if (end > _tickCount) end = _tickCount;

            var brain = _sharedBrain;
            brain.BeginDecisionBatch();

            for (int i = _tickCursor; i < end; i++)
            {
                var e = _tickSnapshot[i];
                if (e == null || e.IsDisposed || e.IsDeathPending) continue;
                e.BeginCycle();
            }

            brain.RunDecisions();
            FlushDeferredDisposals();

            for (int i = _tickCursor; i < end; i++)
            {
                var e = _tickSnapshot[i];
                if (e == null || e.IsDisposed) continue;
                e.EndCycle();
            }

            _planner.Flush();
            _infection.ProcessSpread(_tickSnapshot, _tickCursor, end);

            _transmissions        = _infection.Transmissions;
            _recombinations       = _infection.Recombinations;
            _superinfectionBlocks = _infection.SuperinfectionBlocks;

            _tickCursor = end;

            if (_tickCursor >= _tickCount)
            {
                _tickCursor = 0;
                ProcessPendingDeaths();
                _sharedBrain.ApplyPendingGradients();
                _foodIndex?.PruneUnloaded();
                TickNest();

                float now = SimulationClock.Time;
                if (now >= _nextGenerationTime)
                {
                    if (SimulationRules.Active.PeriodicGenotypeOverwrite) EvolveGenotypes();
                    _nextGenerationTime = now + SimulationRules.Active.GenerationInterval;
                }
            }

            TickFoodRespawn();

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
    }
    private void TickFoodRespawn()
    {
        if (_foodIndex == null || maxFood <= 0) return;

        var rules = SimulationRules.Active;
        float now = SimulationClock.Time;
        float dt  = now - _lastEcologyTime;
        if (dt < rules.EcologyTickInterval) return;
        _lastEcologyTime = now;
        if (dt > rules.EcologyMaxStep) dt = rules.EcologyMaxStep;

        float season = 1f + rules.SeasonAmplitude * Mathf.Sin(2f * Mathf.PI * now / Mathf.Max(rules.SeasonPeriod, 1f));
        _foodIndex.TickEcology(dt, SimulationClock.TimeD, season, maxFood);
    }

    private void TickNest()
    {
        if (_nest == null) return;
        float now = SimulationClock.Time;
        float dt  = now - _lastNestTime;
        _lastNestTime = now;

        int members = 0;
        for (int i = 0; i < _entities.Count; i++)
            if (_entities[i].IsAtNest) members++;

        _nest.Tick(dt, members);
    }

    public EntityModel SpawnEntity(Vector3 position, EntityBrainContext inheritedCtx = null, EntityModel parent = null)
    {
        if (_entities.Count >= maxPopulation)
        {
            inheritedCtx?.Dispose();
            return null;
        }

        var go  = Instantiate(entityPrefab, position, Quaternion.identity, transform);
        var ctx = inheritedCtx ?? _sharedBrain.CreateContext();

        foreach (var expressible in go.GetComponents<IGeneticPhenotype>())
            expressible.ApplyGeneticPhenotype(ctx.CoordMLP.Params);

        var netObj = go.GetComponent<NetworkObject>();
        netObj?.Spawn();

        var viewModel = go.GetComponent<EntityViewModel>();
        var view      = go.GetComponent<EntityView>();

        viewModel.BindMotion(_motion);

        var model = new EntityModel();

        model.Configure(_sharedBrain, ctx, viewModel, viewModel, go, tuning, parent);
        model.Init();
        viewModel.Initialize(model);
        view.Initialize(viewModel);
        model.AddComponentToEntity(new VisualCullingComponent(go, view.LabelObject));
        model.SetUp();

        model.GetEntityComponent<MortalityComponent>().Died += () => HandleEntityDied(model);
        model.OnReproduceRequest += SpawnChild;

        model.ManagerIndex = _entities.Count;
        _entities.Add(model);
        OnEntitySpawned?.Invoke(model);
        return model;
    }

    public void SetEntityVisible(EntityModel model, bool visible)
    {
        if (model == null || model.IsDisposed) return;
        model.GetEntityComponent<VisualCullingComponent>()?.SetVisible(visible);
    }

    public void SetEntitiesVisible(IReadOnlyList<EntityModel> targets, bool visible)
    {
        for (int i = 0; i < targets.Count; i++)
            SetEntityVisible(targets[i], visible);
    }
    public void SpawnChild(EntityModel parent)
    {
        if (_entities.Count >= maxPopulation) return;

        var parentParams = parent.Brain.Context.CoordMLP.Params;
        var mate         = SelectMateByTournament(parent);

        GeneticParameters childParams;
        if (mate != null)
        {
            var mateParams = mate.Brain.Context.CoordMLP.Params;
            GeneticParameters.Crossover(in parentParams, in mateParams, out childParams);
        }
        else
        {
            childParams = parentParams.GetMutatedGeneticParameters();
        }

        var childCtx = _sharedBrain.CreateContext(childParams);
        var pos      = parent.Motor.Position()
                     + (Vector3)RavineRandom.GetInsideCircle().normalized * 2f
                     + Vector3.up * 5f;

        SpawnEntity(pos, childCtx, parent);
    }

    private readonly EntityModel[] _mateCandidates = new EntityModel[InfectionService.MaxNeighbors];

    private EntityModel SelectMateByTournament(EntityModel self)
    {
        int found = self.Perception.FindEntitiesInRadius(self.Motor.Position(), self, _mateCandidates);
        if (found == 0) return null;

        int size = SimulationRules.Active.TournamentSize;
        if (size < 1) size = 1;

        EntityModel best = null;
        float bestFit = float.NegativeInfinity;

        for (int i = 0; i < size; i++)
        {
            var cand = _mateCandidates[_tournamentRng.Range(0, found)];
            if (cand == null || cand.IsDisposed || cand.IsDeathPending) continue;

            float f = cand.GetFitness();
            if (f > bestFit) { bestFit = f; best = cand; }
        }

        Array.Clear(_mateCandidates, 0, found);
        return best;
    }

    public bool SpawnFood()
    {
        if (_foodIndex == null || _foodIndex.FoodCount >= maxFood) return false;

        Vector3 origin = transform.position;

        for (int i = 0; i < FoodSpawnAttempts; i++)
        {
            var v = RavineRandom.GetInsideCircle(spawnRadius);

            int cellX = Mathf.FloorToInt((origin.x + v.x) / MapGenerator.scale);
            int cellZ = Mathf.FloorToInt((origin.z + v.y) / MapGenerator.scale);

            if (_foodIndex.TryAddFood(cellX, cellZ, _foodIndex.RollKind(0, SimulationRules.Active))) return true;
        }
        return false;
    }
    private Vector3 RandomPosition()
    {
        var v = RavineRandom.GetInsideCircle(spawnRadius);
        return transform.position + new Vector3(v.x, 0f, v.y);
    }

    private void HandleEntityDied(EntityModel model)
    {
        if (model == null || model.IsDisposed || model.IsDeathPending) return;
        model.DeathPosition = model.Position2D;
        model.MarkDeathPending();
        _pendingDeath.Enqueue(model);
    }

    private void RebuildTickSnapshot()
    {
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
    }

    private EntityModel[] _deferredDispose = Array.Empty<EntityModel>();
    private int _deferredCount;

    private void ProcessPendingDeaths()
    {
        while (_pendingDeath.Count > 0)
        {
            var model = _pendingDeath.Dequeue();
            if (model == null || model.IsDisposed) continue;

            model.OnReproduceRequest -= SpawnChild;
            model.CaptureFinalFitness();
            DropCorpse(model);
            model.Brain?.CompleteTerminal(TerminalPenaltyFor(model));
            RemoveEntitySwapBack(model);
            OnEntityDied?.Invoke(model);

            if (_deferredCount == _deferredDispose.Length)
                Array.Resize(ref _deferredDispose, Math.Max(_deferredDispose.Length << 1, 8));
            _deferredDispose[_deferredCount++] = model;
        }
    }

    private void DropCorpse(EntityModel model)
    {
        if (_foodIndex == null || model.Stats == null || model.Stats.IsDisposed) return;

        var virology = model.Virology;
        ViralPayload payload = default;
        bool infected = virology != null && virology.TryExtractPayload(out payload);

        _foodIndex.TryAddCorpse(model.DeathPosition, model.BodyEnergy, in payload, infected);
    }

    private void FlushDeferredDisposals()
    {
        for (int i = 0; i < _deferredCount; i++)
        {
            _deferredDispose[i].Dispose();
            _deferredDispose[i] = null;
        }
        _deferredCount = 0;
    }

    private void RemoveEntitySwapBack(EntityModel model)
    {
        int idx = model.ManagerIndex;

        if ((uint)idx >= (uint)_entities.Count || !ReferenceEquals(_entities[idx], model))
        {
            idx = _entities.IndexOf(model);
            if (idx < 0) { model.ManagerIndex = -1; return; }
        }

        int last = _entities.Count - 1;
        if (idx != last)
        {
            var moved = _entities[last];
            _entities[idx]    = moved;
            moved.ManagerIndex = idx;
        }

        _entities.RemoveAt(last);
        model.ManagerIndex = -1;
    }

    private float TerminalPenaltyFor(EntityModel model)
    {
        var rules = SimulationRules.Active;
        float basePenalty = rules.TerminalPenalty;
        if (!_fitnessStatsReady) return basePenalty;

        float z     = (model.FinalFitness - _fitnessMedian) / _fitnessSpread;
        float scale = Mathf.Clamp(1f - z * rules.TerminalFitnessSensitivity,
                                  rules.TerminalPenaltyMinScale,
                                  rules.TerminalPenaltyMaxScale);
        return basePenalty * scale;
    }

    [ContextMenu("Seed Random Strain")]
    private void SeedRandomStrain()
    {
        if (_entities.Count == 0 || _infection == null) return;
        var victim = _entities[RavineRandom.RangeInt(0, _entities.Count)];
        _infection.InjectStrain(victim, 12, (uint)RavineRandom.RangeInt(1, int.MaxValue));
    }

    private async UniTaskVoid TrackDiagnosticsAsync(System.Threading.CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            int n = _entities.Count;
            _entityCount = n;
            _foodCount   = _foodIndex != null ? _foodIndex.FoodCount : 0;

            if (n > 0)
            {
                if (n > _fitnessScratch.Length)
                {
                    int cap = _fitnessScratch.Length;
                    while (cap < n) cap <<= 1;
                    _fitnessScratch = new float[cap];
                }

                float entropySum = 0f;
                float fitnessSum = 0f;

                for (int i = 0; i < n; i++)
                {
                    var e = _entities[i];
                    float f = e.GetFitness();
                    _fitnessScratch[i] = f;
                    fitnessSum += f;
                    entropySum += _sharedBrain.GetCoordinatorEntropy(e.Brain.Context);
                }

                _avgEntropy = entropySum / n;
                _avgFitness = fitnessSum / n;

                Array.Sort(_fitnessScratch, 0, n);

                _fitnessMedian = _fitnessScratch[n >> 1];
                float iqr = _fitnessScratch[(3 * n) >> 2] - _fitnessScratch[n >> 2];
                _fitnessSpread = iqr > 1e-3f
                    ? iqr
                    : Mathf.Max(1e-3f, Mathf.Abs(_fitnessMedian) * 0.25f);
                _fitnessStatsReady = true;
                _sharedBrain?.ReportMeanFitness(_avgFitness);
            }
            else
            {
                _fitnessStatsReady = false;
            }

            await UniTask.Delay(1000, cancellationToken: ct);
        }
    }

    public void EvolveGenotypes()
    {
        if (_tickCursor != 0) return;

        int n = _entities.Count;
        if (n < 2) return;

        if (n > _evolveIndices.Length)
        {
            int cap = _evolveIndices.Length;
            while (cap < n) cap <<= 1;
            _evolveIndices = new int[cap];
            _evolveKeys    = new float[cap];
        }

        for (int i = 0; i < n; i++)
        {
            _evolveIndices[i] = i;
            _evolveKeys[i]    = _entities[i].GetFitness();
        }

        Array.Sort(_evolveKeys, _evolveIndices, 0, n);

        int eliteCount = Mathf.Max(1, (int)(n * SimulationRules.Active.EliteFraction));
        if (eliteCount >= n) eliteCount = n - 1;

        int eliteStart = n - eliteCount;

        for (int r = 0; r < eliteStart; r++)
        {
            var target = _entities[_evolveIndices[r]];
            if (target.IsDisposed || target.IsDeathPending) continue;

            var pa = _entities[_evolveIndices[eliteStart + _tournamentRng.Range(0, eliteCount)]]
                     .Brain.Context.CoordMLP.Params;
            var pb = _entities[_evolveIndices[eliteStart + _tournamentRng.Range(0, eliteCount)]]
                     .Brain.Context.CoordMLP.Params;

            GeneticParameters.Crossover(in pa, in pb, out var child);

            var ctx = target.Brain.Context;
            var own = ctx.CoordMLP.Params;
            GeneticParameters.CopyLearningGenes(in child, ref own);

            ctx.CoordMLP.Params = own;
            for (int g = 0; g < ctx.ExecMLPs.Length; g++)
                ctx.ExecMLPs[g].Params = own;
        }
    }

    private void OnDestroy()
    {
        _infection?.Dispose();
        _tickCts?.Cancel();
        _tickCts?.Dispose();
        ProcessPendingDeaths();
        FlushDeferredDisposals();
        _planner?.Dispose();
        _nest?.Dispose();
        _motion?.Dispose();
        _sharedBrain?.Dispose();
        ContextSlabs.DisposeAll();
    }
}