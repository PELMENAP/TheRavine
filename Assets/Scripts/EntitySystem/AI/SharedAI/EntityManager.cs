using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using R3;
using UnityEngine;
using TheRavine.Extensions;

public class EntityManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject entityPrefab;
    [SerializeField] private GameObject foodPrefab;

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
    [SerializeField, ReadOnly] private int  _entityCount;
    [SerializeField, ReadOnly] private int  _foodCount;
    [SerializeField, ReadOnly] private float _avgEntropy;

    private SharedHierarchicalBrain _sharedBrain;
    private InputVectorizer         _vectorizer;

    private readonly ReactiveProperty<float> _globalMaxHealth = new(200f);
    private readonly ReactiveProperty<float> _globalMaxEnergy = new(200f);

    private readonly List<Entity2D> _entities = new();

    public SharedHierarchicalBrain SharedBrain => _sharedBrain;
    public InputVectorizer         Vectorizer  => _vectorizer;
    public IReadOnlyList<Entity2D> Entities    => _entities;

    private void Awake()
    {
        _vectorizer  = new InputVectorizer(_globalMaxHealth, _globalMaxEnergy);
        _sharedBrain = new SharedHierarchicalBrain(InputVectorizer.VectorSize, lstmHidden);
    }

    private void Start()
    {
        for (int i = 0; i < initialCount; i++)
            SpawnEntity(RandomPosition());

        for (int i = 0; i < initialFood; i++)
            SpawnFood();

        TrackDiagnosticsAsync(destroyCancellationToken).Forget();
    }

    public Entity2D SpawnEntity(Vector3 position, EntityBrainContext inheritedCtx = null)
    {
        if (_entities.Count >= maxPopulation) return null;

        var go     = Instantiate(entityPrefab, position, Quaternion.identity, transform);
        var entity = go.GetComponent<Entity2D>();

        var ctx = inheritedCtx ?? _sharedBrain.CreateContext();
        entity.Inject(_sharedBrain, _vectorizer, ctx, this);

        _entities.Add(entity);
        entity.OnDied             += HandleEntityDied;
        entity.OnReproduceRequest += HandleReproduceRequest;

        entity.SetUpAsNew();

        return entity;
    }

    public Entity2D SpawnChild(Entity2D parent)
    {
        if (_entities.Count >= maxPopulation) return null;

        var childParams = parent.BrainContext.CoordMLP.Params.GetMutatedGeneticParameters();
        var childCtx    = _sharedBrain.CreateContext(childParams);
        var pos         = (Vector2)parent.transform.position
                        + RavineRandom.GetInsideCircle().normalized * 2f;

        return SpawnEntity(pos, childCtx);
    }

    public Entity2D SpawnCrossoverChild(Entity2D parentA, Entity2D parentB)
    {
        if (_entities.Count >= maxPopulation) return null;

        var paramsA     = parentA.BrainContext.CoordMLP.Params;
        var paramsB     = parentB.BrainContext.CoordMLP.Params;
        var childParams = CrossoverGeneticParams(paramsA, paramsB);
        var childCtx    = _sharedBrain.CreateContext(childParams);

        var pos = ((Vector2)parentA.transform.position + (Vector2)parentB.transform.position) * 0.5f;
        return SpawnEntity(pos, childCtx);
    }

    public void SpawnFood()
    {
        if (_foodCount >= maxFood || foodPrefab == null) return;
        Instantiate(foodPrefab, RandomPosition(), Quaternion.identity, transform);
        _foodCount++;
    }

    private void HandleEntityDied(Entity2D entity)
    {
        _entities.Remove(entity);
        entity.OnDied             -= HandleEntityDied;
        entity.OnReproduceRequest -= HandleReproduceRequest;

        if (_entities.Count < initialCount / 2)
            SpawnEntity(RandomPosition());
    }

    private void HandleReproduceRequest(Entity2D parent) => SpawnChild(parent);

    [Button]
    public void EvolveSharedWeights()
    {
        if (_entities.Count < 2) return;

        _entities.Sort((a, b) =>
            b.BrainContext.CoordMLP.AverageEntropy
                .CompareTo(a.BrainContext.CoordMLP.AverageEntropy));

        int eliteCount = Math.Max(1, _entities.Count / 10);
        for (int i = eliteCount; i < _entities.Count; i++)
        {
            int parentIdx = RavineRandom.RangeInt(0, eliteCount);
            var childParams = _entities[parentIdx].BrainContext.CoordMLP.Params
                                                   .GetMutatedGeneticParameters();
            _entities[i].BrainContext.CoordMLP.Params = childParams;
            _entities[i].BrainContext.ResetMemory();
        }
    }

    private Vector3 RandomPosition()
    {
        var v = RavineRandom.GetInsideCircle() * spawnRadius;
        return transform.position + new Vector3(v.x, v.y, 0f);
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
            MutationStrength      = RavineRandom.RangeBool() ? pA.MutationStrength      : pB.MutationStrength,
            BaseDelta             = RavineRandom.RangeBool() ? pA.BaseDelta             : pB.BaseDelta,
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
                    sum += _sharedBrain.GetCoordinatorEntropy(e.BrainContext);
                _avgEntropy = sum / _entities.Count;
            }
            await UniTask.Delay(1000, cancellationToken: ct);
        }
    }
}