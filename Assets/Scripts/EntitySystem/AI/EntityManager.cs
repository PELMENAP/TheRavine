using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

using TheRavine.Generator;

public class EntityManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject entityPrefab;
    [SerializeField] private GameObject foodPrefab;
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

    public int MaxPopulation => maxPopulation;
    public event Action<EntityModel> OnEntitySpawned;
    public event Action<EntityModel> OnEntityDied;

    public void OnFoodConsumed() => _foodCount--;

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

    private void Update() => SimulationClock.Advance(Time.deltaTime * simulationTimeScale);

    private void Awake()
    {
        SimulationRules.Bind(rules);
        NeuralModelStorage.RegisterFactory(new SharedBrainSnapshotFactory());
        _sharedBrain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, lstmHidden);
        // LoadBrain();
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

            _tickCursor = end;

            if (_tickCursor >= _tickCount)
            {
                _tickCursor = 0;
                _sharedBrain.ApplyPendingGradients();
                ProcessPendingDeaths();
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
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
    }

    public EntityModel SpawnEntity(Vector3 position, EntityBrainContext inheritedCtx = null)
    {
        if (_entities.Count >= maxPopulation) return null;

        var go = Instantiate(entityPrefab, position, Quaternion.identity, transform);
        var ctx = inheritedCtx ?? _sharedBrain.CreateContext();

        foreach (var expressible in go.GetComponents<IGeneticPhenotype>())
            expressible.ApplyGeneticPhenotype(ctx.CoordMLP.Params);

        var netObj = go.GetComponent<NetworkObject>();
        netObj?.Spawn();

        var viewModel = go.GetComponent<EntityViewModel>();
        var view = go.GetComponent<EntityView>();

        var model = new EntityModel();

        model.Configure(_sharedBrain, ctx, viewModel, viewModel, go, tuning);
        model.Init();
        viewModel.Initialize(model);
        view.Initialize(viewModel);
        model.AddComponentToEntity(new VisualCullingComponent(go, view.LabelObject));
        model.SetUp();

        model.GetEntityComponent<MortalityComponent>().Died += () => HandleEntityDied(model);
        model.OnReproduceRequest += SpawnChild;

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

        var childParams = parent.Brain.Context.CoordMLP.Params.GetMutatedGeneticParameters();
        var childCtx    = _sharedBrain.CreateContext(childParams);
        var pos         = parent.Motor.Position()
                        + (Vector3)RavineRandom.GetInsideCircle().normalized * 2f
                        + Vector3.up * 5f;

        SpawnEntity(pos, childCtx);
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
        if (_foodIndex == null || _foodIndex.FoodCount >= maxFood) return false;

        Vector3 origin = transform.position;

        for (int i = 0; i < FoodSpawnAttempts; i++)
        {
            var v = RavineRandom.GetInsideSphere(spawnRadius);

            int cellX = Mathf.FloorToInt((origin.x + v.x) / MapGenerator.scale);
            int cellZ = Mathf.FloorToInt((origin.z + v.y) / MapGenerator.scale);

            if (_foodIndex.TryAddFood(cellX, cellZ, 1)) return true;
        }
        return false;
    }

    private void HandleEntityDied(EntityModel model)
    {
        if (model == null || model.IsDisposed || model.IsDeathPending) return;
        model.MarkDeathPending();
        _pendingDeath.Enqueue(model);
    }

    private void ProcessPendingDeaths()
    {
        while (_pendingDeath.Count > 0)
        {
            var model = _pendingDeath.Dequeue();
            if (model == null || model.IsDisposed) continue;

            model.OnReproduceRequest -= SpawnChild;
            model.CaptureFinalFitness();
            model.Brain?.CompleteTerminal(SimulationRules.Active.TerminalPenalty);

            _entities.Remove(model);
            OnEntityDied?.Invoke(model);
            model.Dispose();
        }
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

    private Vector3 RandomPosition()
    {
        var v = RavineRandom.GetInsideSphere(spawnRadius);
        return transform.position + new Vector3(v.x, 0, v.y);
    }

    private static GeneticParameters CrossoverGeneticParams(GeneticParameters a, GeneticParameters b)
    {
        GeneticParameters.Crossover(in a, in b, out var child);
        return child;
    }

    private async UniTaskVoid TrackDiagnosticsAsync(System.Threading.CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _entityCount = _entities.Count;
            _foodCount   = _foodIndex != null ? _foodIndex.FoodCount : 0;

            if (_entities.Count > 0)
            {
                float sum = 0f;
                foreach (var e in _entities)
                    sum += _sharedBrain.GetCoordinatorEntropy(e.Brain.Context);
                _avgEntropy = sum / _entities.Count;
            }
            await UniTask.Delay(1000, cancellationToken: ct);
        }
    }

    private void OnDestroy()
    {
        _tickCts?.Cancel();
        _tickCts?.Dispose();
        ProcessPendingDeaths();
    }
}