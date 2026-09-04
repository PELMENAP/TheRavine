using Cysharp.Threading.Tasks;
using UnityEngine;
using TheRavine.EntityControl;
using System;
using System.Collections.Generic;

public class EntityModel : AEntity
{
    private const float FitnessTimeAliveWeight = 1f;
    private const float FitnessFoodEatenWeight = 15f;
    private const float FitnessReproduceWeight = 40f;
    private const float FitnessDamageDealtWeight = 2f;

    public enum FitnessEvent { FoodEaten, Reproduced, DamageDealt }

    public StatsComponent Stats { get; private set; }
    public PerceptionComponent Perception { get; private set; }
    public BrainComponent Brain { get; private set; }
    public SpeechComponent Speech { get; private set; }
    public PointsOfInterestComponent Points { get; private set; }
    public IEntityDialogHost DialogHost { get; private set; }

    public IEntityMotor Motor { get; private set; }
    public EntityTuning Tuning { get; private set; }
    public GameObject SelfObject { get; private set; }

    private StatePatternComponent states;
    public InputVectorizer Vectorizer;

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

    public bool IsDeathPending { get; private set; }
    public void MarkDeathPending() => IsDeathPending = true;

    public void RegisterFitnessEvent(FitnessEvent evt, float amount = 0f)
    {
        switch (evt)
        {
            case FitnessEvent.FoodEaten: FoodEaten++; break;
            case FitnessEvent.Reproduced: ReproduceCount++; break;
            case FitnessEvent.DamageDealt: DamageDealt += amount; break;
        }
    }

    public float GetFitness() =>
        TimeAlive * FitnessTimeAliveWeight +
        FoodEaten * FitnessFoodEatenWeight +
        ReproduceCount * FitnessReproduceWeight +
        DamageDealt * FitnessDamageDealtWeight;

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

        AddComponentToEntity(new PerceptionComponent(tuning.DetectionRadius, tuning.EntityLayer, tuning.FoodLayer));
        Perception = GetEntityComponent<PerceptionComponent>();

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
    }

    public bool TryStartAttackCooldown()
    {
        if (!canAttack) return false;
        canAttack = false;
        CooldownAsync().Forget();
        return true;
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

    public override void SetUp() =>
        states.SetBehaviourAsync(states.GetBehaviour<SurviveState>()).Forget();

    private float _lastCycleTime;
    public override void UpdateEntityCycle()
    {
        if (IsDisposed || IsDeathPending) return;
        if (!IsActive.Value) return;
        if (Stats.IsDisposed || Stats.Health.Value <= 0f) return;

        float now = SimulationClock.Time;
        float dt  = now - _lastCycleTime;
        if (dt <= 0f) dt = 1f / 60f;
        _lastCycleTime = now;

        TimeAlive += dt;
        timeOfDay = (timeOfDay + 1) % 24;

        float inDanger = ComputeDangerLevel();
        float timeToBreed = ComputeBreedReadiness();
        Perception.FindNearestEntity(Motor.Position(), SelfObject, out float enemyDist);
        Perception.FindNearestFood(Motor.Position(), out float foodDist);

        LastInput = Vectorizer.Vectorize(
            Stats.Health.Value, Stats.Energy.Value,
            LastActionIndex, timeOfDay, inDanger, timeToBreed,
            Speech.OtherSpeech, enemyDist, foodDist);

        Speech.ConsumeOtherSpeech();

        bool isIdle = states.behaviourCurrent.GetType() == typeof(SurviveState)
                && LastActionIndex == (int)EntityAction.Idle;

        var p = Brain.Context.CoordMLP.Params;
        Stats.Tick(dt, Tuning.EnergyRegenRate, isIdle,
            p.StarvationThreshold, p.StarvationDamage, p.StarvationEnergyReturn);

        if (IsDeathPending || IsDisposed || Stats.IsDisposed || Stats.Health.Value <= 0f) return;

        if (Brain.TryDecide(LastInput, now, dt, out var decision))
        {
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