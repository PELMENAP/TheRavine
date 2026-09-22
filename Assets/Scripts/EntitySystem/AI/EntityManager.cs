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
    [SerializeField] private TheRavine.EntityControl.Virology.StrainCodonTable strainCodonTable;

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
    private float _foodBudget;
    private float _lastFoodTime;

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
        && _infection.InjectRecipe(target, recipe, _virologyTick, (uint)RavineRandom.RangeInt(1, int.MaxValue));

    private void Update() => SimulationClock.Advance(Time.deltaTime * simulationTimeScale);

    private void Awake()
    {
        ServiceLocator.Services.Register(new ChunkFoodIndex(ServiceLocator.Services.Get<MapGenerator>()));

        SimulationRules.Bind(rules);
        ServiceLocator.Services.Register(_grid);
        NeuralModelStorage.RegisterFactory(new SharedBrainSnapshotFactory());
        _infection = new InfectionService((uint)UnityEngine.Random.Range(1, int.MaxValue));
        _tournamentRng = new XorShift32((uint)UnityEngine.Random.Range(1, int.MaxValue));

        // _sharedBrain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, lstmHidden);
        LoadBrain();
    }

    [ContextMenu("Save Brain")]
    private void SaveBrain() => SaveBestBrainAsync().Forget();

    [ContextMenu("Load Brain")]
    private void LoadBrain() => LoadBrainAsync().Forget();

    private async UniTaskVoid SaveBestBrainAsync()
    {
        await NeuralModelStorage.SaveAsync(_sharedBrain.ToSnapshot(), savedModelName, destroyCancellationToken);
    }

    private async UniTaskVoid LoadBrainAsync()
    {
        var snapshot = await NeuralModelStorage.LoadAsync<SharedBrainSnapshot>(savedModelName, destroyCancellationToken);
        var brain = SharedHierarchicalBrain.FromSnapshot(snapshot, InputVectorizer.VectorSize, lstmHidden);
        if (brain == null) return;

        _sharedBrain = brain;
        for (int i = 0; i < _entities.Count; i++)
            if (!_entities[i].IsDisposed)
                _entities[i].Brain.ReplaceBrain(brain);
    }
    private CancellationTokenSource _tickCts;

    
    private async void Start()
    {
        await UniTask.Delay(3000);

        _foodIndex = new ChunkFoodIndex(await ServiceLocator.WaitUntilServiceReady<MapGenerator>());
        ServiceLocator.Services.Register(_foodIndex);

        for (int i = 0; i < initialCount; i++)
            SpawnEntity(RandomPosition());

        for (int i = 0; i < initialFood; i++)
            SpawnFood();

        _nextGenerationTime = SimulationClock.Time + SimulationRules.Active.GenerationInterval;
        _lastFoodTime       = SimulationClock.Time;

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
                TickFoodRespawn();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                continue;
            }

            int end = _tickCursor + _tickBatch;
            if (end > _tickCount) end = _tickCount;

            for (int i = _tickCursor; i < end; i++)
            {
                var e = _tickSnapshot[i];
                if (e == null || e.IsDisposed || e.IsDeathPending) continue;
                e.UpdateEntityCycle();
            }

            _virologyTick++;
            _infection.ProcessSpread(_tickSnapshot, _tickCursor, end, _virologyTick);

            _transmissions        = _infection.Transmissions;
            _recombinations       = _infection.Recombinations;
            _superinfectionBlocks = _infection.SuperinfectionBlocks;

            _tickCursor = end;

            if (_tickCursor >= _tickCount)
            {
                _tickCursor = 0;
                _sharedBrain.ApplyPendingGradients();
                ProcessPendingDeaths();

                float now = SimulationClock.Time;
                if (now >= _nextGenerationTime)
                {
                    EvolveGenotypes();
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

        ref readonly var rules = ref SimulationRules.Frame;

        float now   = SimulationClock.Time;
        float dt    = now - _lastFoodTime;
        _lastFoodTime = now;

        if (dt <= 0f) return;
        if (dt > rules.FoodRegenMaxStep) dt = rules.FoodRegenMaxStep;

        int count = _foodIndex.FoodCount;
        if (count >= maxFood) { _foodBudget = 0f; return; }

        float headroom = 1f - (float)count / maxFood;
        _foodBudget += rules.FoodRegenPerSecond * dt * headroom;

        int burst = rules.FoodRegenBurst;
        int whole = (int)_foodBudget;
        if (whole <= 0) return;
        if (whole > burst) whole = burst;

        for (int i = 0; i < whole; i++)
        {
            if (!SpawnFood()) break;
            _foodBudget -= 1f;
        }

        if (_foodBudget > burst) _foodBudget = burst;
    }

    public EntityModel SpawnEntity(Vector3 position, EntityBrainContext inheritedCtx = null)
    {
        if (_entities.Count >= maxPopulation) return null;

        var go  = Instantiate(entityPrefab, position, Quaternion.identity, transform);
        var ctx = inheritedCtx ?? _sharedBrain.CreateContext();

        foreach (var expressible in go.GetComponents<IGeneticPhenotype>())
            expressible.ApplyGeneticPhenotype(ctx.CoordMLP.Params);

        var netObj = go.GetComponent<NetworkObject>();
        netObj?.Spawn();

        var viewModel = go.GetComponent<EntityViewModel>();
        var view      = go.GetComponent<EntityView>();

        var model = new EntityModel();

        model.Configure(_sharedBrain, ctx, viewModel, viewModel, go, tuning);
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

        SpawnEntity(pos, childCtx);
    }

    private EntityModel SelectMateByTournament(EntityModel exclude)
    {
        int n = _entities.Count;
        if (n < 2) return null;

        int size = SimulationRules.Active.TournamentSize;
        if (size < 2) size = 2;

        EntityModel best = null;
        float bestFit = float.NegativeInfinity;

        for (int i = 0; i < size; i++)
        {
            var cand = _entities[_tournamentRng.Range(0, n)];
            if (cand == null || cand == exclude || cand.IsDisposed || cand.IsDeathPending) continue;

            float f = cand.GetFitness();
            if (f > bestFit) { bestFit = f; best = cand; }
        }
        return best;
    }

    public EntityModel SpawnCrossoverChild(EntityModel parentA, EntityModel parentB)
    {
        if (_entities.Count >= maxPopulation) return null;

        var paramsA     = parentA.Brain.Context.CoordMLP.Params;
        var paramsB     = parentB.Brain.Context.CoordMLP.Params;
        var childParams = CrossoverGeneticParams(paramsA, paramsB);
        var childCtx    = _sharedBrain.CreateContext(childParams);

        var pos = ((Vector2)parentA.Motor.Position() + (Vector2)parentB.Motor.Position()) * 0.5f;
        return SpawnEntity(pos, childCtx);
    }

    public bool SpawnFood()
    {
        Debug.Log("food spawned");

        if (_foodIndex == null || _foodIndex.FoodCount >= maxFood) return false;

        Vector3 origin = transform.position;

        for (int i = 0; i < FoodSpawnAttempts; i++)
        {
            var v = RavineRandom.GetInsideCircle(spawnRadius);

            int cellX = Mathf.FloorToInt((origin.x + v.x) / MapGenerator.scale);
            int cellZ = Mathf.FloorToInt((origin.z + v.y) / MapGenerator.scale);

            if (_foodIndex.TryAddFood(cellX, cellZ, 1)) return true;
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

    private void ProcessPendingDeaths()
    {
        while (_pendingDeath.Count > 0)
        {
            var model = _pendingDeath.Dequeue();
            if (model == null || model.IsDisposed) continue;

            model.OnReproduceRequest -= SpawnChild;
            model.CaptureFinalFitness();
            model.Brain?.CompleteTerminal(TerminalPenaltyFor(model));

            RemoveEntitySwapBack(model);
            OnEntityDied?.Invoke(model);
            model.Dispose();
        }
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

    public void EvolveSharedWeights()
    {
        if (_entities.Count < 2) return;

        _entities.Sort((a, b) => b.GetFitness().CompareTo(a.GetFitness()));

        int eliteCount = Math.Max(1, _entities.Count / 10);
        for (int i = eliteCount; i < _entities.Count; i++)
        {
            int parentIdx = RavineRandom.RangeInt(0, eliteCount);
            var childParams = _entities[parentIdx].Brain.Context.CoordMLP.Params
                                                .GetMutatedGeneticParameters();
            _entities[i].Brain.Context.CoordMLP.Params = childParams;
            _entities[i].Brain.Context.ResetMemory();
        }
    }

    private static GeneticParameters CrossoverGeneticParams(GeneticParameters a, GeneticParameters b)
    {
        GeneticParameters.Crossover(in a, in b, out var child);
        return child;
    }

    [ContextMenu("Seed Random Strain")]
    private void SeedRandomStrain()
    {
        if (_entities.Count == 0 || _infection == null) return;
        var victim = _entities[RavineRandom.RangeInt(0, _entities.Count)];
        _infection.InjectStrain(victim, 12, (uint)RavineRandom.RangeInt(1, int.MaxValue), _virologyTick);
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
            ctx.CoordMLP.Params = child;
            for (int g = 0; g < ctx.ExecMLPs.Length; g++)
                ctx.ExecMLPs[g].Params = child;

            ctx.ResetMemory();
        }
    }

    private void OnDestroy()
    {
        _infection?.Dispose();
        _tickCts?.Cancel();
        _tickCts?.Dispose();
        ProcessPendingDeaths();
    }
}