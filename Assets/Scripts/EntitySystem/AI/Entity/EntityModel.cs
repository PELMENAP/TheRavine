using Cysharp.Threading.Tasks;
using UnityEngine;
using TheRavine.EntityControl;
using System;
using System.Collections.Generic;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public class EntityModel : AEntity
{

    public enum FitnessEvent { FoodEaten, Reproduced, DamageDealt }

    public StatsComponent Stats { get; private set; }
    public PerceptionComponent Perception { get; private set; }
    public BrainComponent Brain { get; private set; }
    public SpeechComponent Speech { get; private set; }
    public PointsOfInterestComponent Points { get; private set; }
    public VirologyComponent Virology { get; private set; }

    public IEntityDialogHost DialogHost { get; private set; }

    public IEntityMotor Motor { get; private set; }
    public EntityTuning Tuning { get; private set; }
    public GameObject SelfObject { get; private set; }

    private StatePatternComponent states;
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
    }
    private int timeOfDay;
    private bool canAttack = true;

    public float TimeAlive { get; private set; }
    public int FoodEaten { get; private set; }
    public int ReproduceCount { get; private set; }
    public float DamageDealt { get; private set; }
    public float FinalFitness { get; private set; }

    private R3.ReactiveProperty<float> _vecMaxHealth;
    private R3.ReactiveProperty<float> _vecMaxEnergy;

    private TerrainSensor _terrain;
    private ChunkFoodIndex _foodIndex;
    private TerrainSample _lastTerrain = TerrainSample.Invalid;

    public ref readonly TerrainSample LastTerrain => ref _lastTerrain;

    public bool IsDeathPending { get; private set; }
    public void MarkDeathPending() => IsDeathPending = true;

    public int ManagerIndex = -1;

    private readonly float[] _decisionInput = new float[InputVectorizer.VectorSize];
    public float[] DecisionInput => _decisionInput;

    private EntitySpatialGrid _grid;

    private long  _foodCell;
    private float _foodDistance = -1f;
    private bool  _foodValid;

    public bool  CachedFoodValid    => _foodValid;
    public long  CachedFoodCell     => _foodCell;
    public float CachedFoodDistance => _foodDistance;
    public void  InvalidateCachedFood() => _foodValid = false;

    private float _fitnessCache;
    private float _fitnessCacheTime = float.NaN;
    private int   _fitnessEpoch;
    private int   _fitnessCacheEpoch = -1;

    private double    _attackReadyTime;
    private double    _lastCycleTime;
    private DayCycle  _dayCycle;

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
        states.SetBehaviourAsync(states.GetBehaviour<SurviveState>()).Forget();
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
                       * math.log(1f + life / math.max(rules.FitnessSurvivalTau, 1e-3f));

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

    private static readonly Dictionary<SharedHierarchicalBrain.Goal, Type> GoalStateMap = new()
    {
        [SharedHierarchicalBrain.Goal.Survive] = typeof(SurviveState),
        [SharedHierarchicalBrain.Goal.Hunt]    = typeof(HuntState),
        [SharedHierarchicalBrain.Goal.Forage]  = typeof(ForageState),
        [SharedHierarchicalBrain.Goal.Social]  = typeof(SocialState),
    };
    public ChunkFoodIndex FoodIndex => _foodIndex;

    public void Configure(
        SharedHierarchicalBrain brain, EntityBrainContext ctx,
        IEntityMotor motor, IEntityDeathHandler death,
        GameObject selfObject, EntityTuning tuning)
    {
        Motor = motor;
        SelfObject = selfObject;
        Tuning = tuning;
        DialogHost = (IEntityDialogHost)motor;

        Stats = GetOrCreateEntityComponent<StatsComponent>();
        Stats.FillComponent(tuning.MaxHealth, tuning.MaxEnergy);

        ServiceLocator.Services.TryGet(out _grid);
        AddComponentToEntity(new PerceptionComponent(tuning.DetectionRadius, _grid));
        Perception = GetEntityComponent<PerceptionComponent>();

        _terrain = new TerrainSensor(ServiceLocator.GetService<MapGenerator>());
        ServiceLocator.Services.TryGet(out _foodIndex);

        Speech = GetOrCreateEntityComponent<SpeechComponent>();
        Speech.Inject((IEntityAudio)motor);
        Points = GetOrCreateEntityComponent<PointsOfInterestComponent>();

        AddComponentToEntity(new BrainComponent(brain, ctx));
        Brain = GetEntityComponent<BrainComponent>();

        AddComponentToEntity(new MortalityComponent(Stats.Health));
        GetEntityComponent<MortalityComponent>().Died += CancelCurrentCommand;
        GetEntityComponent<MortalityComponent>().Died += () => death?.OnDeath();
        states = GetOrCreateEntityComponent<StatePatternComponent>();

        _vecMaxHealth = new R3.ReactiveProperty<float>(tuning.MaxHealth);
        _vecMaxEnergy = new R3.ReactiveProperty<float>(tuning.MaxEnergy);
        Vectorizer = new InputVectorizer(_vecMaxHealth, _vecMaxEnergy);

        Virology = GetOrCreateEntityComponent<VirologyComponent>();
        Virology.FillComponent(ctx.CoordMLP.Params, ctx.CoordMLP.Params.ComputeHash());
    }


    private async UniTaskVoid CooldownAsync()
    {
        await UniTask.Delay((int)(Tuning.AttackCooldown * 1000));
        canAttack = true;
    }

    public override void Init()
    {
        states.AddBehaviour(typeof(SurviveState), new SurviveState(this));
        states.AddBehaviour(typeof(HuntState), new HuntState(this));
        states.AddBehaviour(typeof(ForageState), new ForageState(this));
        states.AddBehaviour(typeof(SocialState), new SocialState(this));
    }
    private float ResolveDayPhase()
    {
        if (_dayCycle == null && !ServiceLocator.Services.TryGet(out _dayCycle)) return 0f;
        return _dayCycle.NormalizedTime.CurrentValue;
    }

    public override void UpdateEntityCycle()
    {
        if (IsDisposed || IsDeathPending) return;
        if (!IsActive.Value) return;
        if (Stats.IsDisposed || Stats.Health.Value <= 0f) return;

        ref readonly var rules = ref SimulationRules.Frame;

        double nowD = SimulationClock.TimeD;
        float  dt   = (float)(nowD - _lastCycleTime);
        _lastCycleTime = nowD;
        if (dt <= 0f) dt = 1f / 60f;
        if (dt > rules.MaxCycleDt) dt = rules.MaxCycleDt;
        float now = (float)nowD;

        TimeAlive += dt;

        Virology.Step(Stats, dt);

        if (IsDeathPending || IsDisposed || Stats.IsDisposed || Stats.Health.Value <= 0f) return;

        float inDanger = ComputeDangerLevel();
        float timeToBreed = ComputeBreedReadiness();

        Vector3 pos = Motor.Position();
        Perception.FindNearestEntity(pos, SelfObject, out float enemyDist);

        _foodValid    = false;
        _foodDistance = -1f;
        if (_foodIndex != null &&
            _foodIndex.TryFindNearestFood(pos.x, pos.z, Tuning.DetectionRadius,
                out _foodCell, out _foodDistance))
            _foodValid = true;

        float foodDist = _foodValid ? _foodDistance : -1f;

        if (!_terrain.TrySample(pos.x, pos.z, out _lastTerrain))
            _lastTerrain = TerrainSample.Invalid;

        Virology.TryGetViralInputs(out float viralLoad, out float viralSegments, out float viralNet);

        LastInput = Vectorizer.Vectorize(
            Stats.Health.Value, Stats.Energy.Value,
            LastActionIndex, ResolveDayPhase(), inDanger, timeToBreed,
            Speech.OtherSpeechHash, enemyDist, foodDist, in _lastTerrain, MimickedActionIndex,
            viralLoad, viralSegments, viralNet);

        Speech.ConsumeOtherSpeech();
        ConsumeMimickedAction();

        bool isIdle = states.behaviourCurrent.GetType() == typeof(SurviveState)
                && LastActionIndex == (int)EntityAction.Idle;

        Stats.Tick(dt,
            Tuning.EnergyRegenRate,
            Virology.Modifiers.RegenMultiplier,
            Virology.Modifiers.MetabolismMultiplier,
            rules.BasalEnergyDrain,
            rules.IdleRegenBasalFraction,
            isIdle,
            rules.StarvationThreshold, rules.StarvationDamage, rules.StarvationEnergyReturn,
            out float regenCredit, out float metabolismCredit);

        Virology.CreditDurableEffects(regenCredit, metabolismCredit, Stats.MaxEnergy);

        if (IsDeathPending || IsDisposed || Stats.IsDisposed || Stats.Health.Value <= 0f) return;

        var brainCtx = Brain.Context;
        brainCtx.CoordBias[0] = Virology.Modifiers.WanderBias;
        brainCtx.CoordBias[1] = Virology.Modifiers.HuntBias;
        brainCtx.CoordBias[2] = Virology.Modifiers.ForageBias;
        brainCtx.CoordBias[3] = Virology.Modifiers.SocialBias;
        brainCtx.FleeBias     = Virology.Modifiers.FleeBias;

        if (Brain.TryDecide(LastInput, now, dt, out var decision))
        {
            Array.Copy(LastInput, _decisionInput, _decisionInput.Length);

            SetLastAction(decision.Action);

            var targetType = GoalStateMap[decision.Goal];
            if (states.behaviourCurrent.GetType() != targetType)
                states.SetBehaviourAsync(states.GetBehaviourByType(targetType)).Forget();

            ((EntityActionState)states.behaviourCurrent).EnqueueAction((EntityAction)decision.Action, decision);
        }

        states.behaviourCurrent.Update();
        OnUpdate.Execute(R3.Unit.Default);
    }

    private float ComputeDangerLevel()
    {
        float d = 0f;
        float hp = Stats.Health.Value, en = Stats.Energy.Value;
        if (en < 50 || hp < 50) d += 0.25f;
        if (en < 25 || hp < 25) d += 0.25f;
        if (en < 10 || hp < 10) d += 0.5f;
        return d;
    }

    private float ComputeBreedReadiness()
    {
        float b = 0f;
        float hp = Stats.Health.Value, en = Stats.Energy.Value;
        if (en > Tuning.ReproduceEnergyCost && hp > Tuning.ReproduceHealthCost) b += 0.5f;
        if (en > Tuning.ReproduceEnergyCost + 50 && hp > Tuning.ReproduceHealthCost + 50) b += 0.5f;
        return b;
    }

    private void CancelCurrentCommand() => states.behaviourCurrent?.CancelCurrentCommand();

    public override void DeepClean()
    {
        Vectorizer?.Dispose();
        Vectorizer = null;
        _vecMaxHealth?.Dispose();
        _vecMaxEnergy?.Dispose();
        _vecMaxHealth = null;
        _vecMaxEnergy = null;
    }
}