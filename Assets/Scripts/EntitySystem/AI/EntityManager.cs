using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;

public class EntityManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject entityPrefab;
    [SerializeField] private GameObject foodPrefab;
    [SerializeField] private EntityTuning tuning;

    [Header("Population")]
    [SerializeField] private int   initialCount  = 20;
    [SerializeField] private int   maxPopulation = 200;
    [SerializeField] private float spawnRadius   = 20f;

    [Header("Food")]
    [SerializeField] private int initialFood = 50;
    [SerializeField] private int maxFood     = 100;

    [Header("Brain")]
    [SerializeField] private int lstmHidden  = 32;

    [Header("Diagnostics")]
    [SerializeField] private int  _entityCount;
    [SerializeField] private int  _foodCount;
    [SerializeField] private float _avgEntropy;

    public int MaxPopulation => maxPopulation;
    public event Action<EntityModel> OnEntitySpawned;
    public event Action<EntityModel> OnEntityDied;

    public void OnFoodConsumed() => _foodCount--;

    private SharedHierarchicalBrain _sharedBrain;
    public SharedHierarchicalBrain SharedBrain => _sharedBrain;
    private readonly List<EntityModel> _entities = new();
    public List<EntityModel> Entities => _entities;

    private void Awake()
    {
        _sharedBrain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, lstmHidden);
    }
    private CancellationTokenSource _tickCts;
    private async void Start()
    {
        await UniTask.Delay(3000);

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
        const float window = 1f;
        while (!ct.IsCancellationRequested)
        {
            int count = _entities.Count;
            if (count == 0) { await UniTask.Delay(TimeSpan.FromSeconds(window), cancellationToken: ct); continue; }

            float stepDelay = window / count;
            for (int i = 0; i < count; i++)
            {
                if (i < _entities.Count) _entities[i].UpdateEntityCycle();
                await UniTask.Delay(TimeSpan.FromSeconds(stepDelay), cancellationToken: ct);
            }
        }
    }

    public EntityModel SpawnEntity(Vector3 position, EntityBrainContext inheritedCtx = null)
    {
        if (_entities.Count >= maxPopulation) return null;

        var go = Instantiate(entityPrefab, position, Quaternion.identity, transform);
        
        var netObj = go.GetComponent<NetworkObject>();
        netObj?.Spawn();

        var viewModel = go.GetComponent<EntityViewModel>();

        var model = new EntityModel();
        var ctx = inheritedCtx ?? _sharedBrain.CreateContext();

        model.Configure(_sharedBrain, ctx, viewModel, viewModel, go, tuning);
        model.Init();
        viewModel.Initialize(model);
        model.SetUp();

        model.GetEntityComponent<MortalityComponent>().Died += () => HandleEntityDied(model);
        model.OnReproduceRequest += SpawnChild;

        _entities.Add(model);
        OnEntitySpawned?.Invoke(model);
        return model;
    }

    public void SpawnChild(EntityModel parent)
    {
        if (_entities.Count >= maxPopulation) return;

        var childParams = parent.Brain.Context.CoordMLP.Params.GetMutatedGeneticParameters();
        var childCtx    = _sharedBrain.CreateContext(childParams);
        var pos         = parent.Motor.Position
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

        var pos = ((Vector2)parentA.Motor.Position + (Vector2)parentB.Motor.Position) * 0.5f;
        return SpawnEntity(pos, childCtx);
    }

    public void SpawnFood()
    {
        if (_foodCount >= maxFood || foodPrefab == null) return;
        var go   = Instantiate(foodPrefab, RandomPosition(), Quaternion.identity, transform);
        var food = go.GetComponent<FoodObject>();
        if (food == null) food = go.AddComponent<FoodObject>();
        food.Init(this);
        _foodCount++;
    }

    private void HandleEntityDied(EntityModel model)
    {
        model.OnReproduceRequest -= SpawnChild;
        _entities.Remove(model);
        model.Dispose();

        if (_entities.Count < initialCount / 2)
            SpawnEntity(RandomPosition());

        OnEntityDied?.Invoke(model);
    }

    public void EvolveSharedWeights()
    {
        if (_entities.Count < 2) return;

        _entities.Sort((a, b) =>
            b.Brain.Context.CoordMLP.AverageEntropy
                .CompareTo(a.Brain.Context.CoordMLP.AverageEntropy));

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
        var pA = a; var pB = b;
        return new GeneticParameters
        {
            DefaultEvaluation     = RavineRandom.RangeBool() ? pA.DefaultEvaluation     : pB.DefaultEvaluation,
            Lambda                = RavineRandom.RangeBool() ? pA.Lambda                : pB.Lambda,
            BaseLearningRate      = RavineRandom.RangeBool() ? pA.BaseLearningRate      : pB.BaseLearningRate,
            MaxGradientNorm       = RavineRandom.RangeBool() ? pA.MaxGradientNorm       : pB.MaxGradientNorm,
            SoftmaxTemperature    = RavineRandom.RangeBool() ? pA.SoftmaxTemperature    : pB.SoftmaxTemperature,
            EntropyRegularization = RavineRandom.RangeBool() ? pA.EntropyRegularization : pB.EntropyRegularization,
            LabelSmoothing        = RavineRandom.RangeBool() ? pA.LabelSmoothing        : pB.LabelSmoothing,
            EntropyAlpha          = RavineRandom.RangeBool() ? pA.EntropyAlpha          : pB.EntropyAlpha,
            InitBiasesValues      = RavineRandom.RangeBool() ? pA.InitBiasesValues      : pB.InitBiasesValues,
            GaussianNoise         = RavineRandom.RangeBool() ? pA.GaussianNoise         : pB.GaussianNoise,
            ExplorationPrice      = RavineRandom.RangeBool() ? pA.ExplorationPrice      : pB.ExplorationPrice,
            MutationChance        = RavineRandom.RangeBool() ? pA.MutationChance        : pB.MutationChance,
        }.GetMutatedGeneticParameters();
    }

    private async UniTaskVoid TrackDiagnosticsAsync(System.Threading.CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _entityCount = _entities.Count;
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
    }
}