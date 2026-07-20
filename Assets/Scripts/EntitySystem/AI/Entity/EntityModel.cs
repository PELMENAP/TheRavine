using Cysharp.Threading.Tasks;
using UnityEngine;
using TheRavine.EntityControl;
using System;
using System.Collections.Generic;

public class EntityModel : AEntity
{
    public StatsComponent Stats { get; private set; }
    public PerceptionComponent Perception { get; private set; }
    public BrainComponent Brain { get; private set; }
    public SpeechComponent Speech { get; private set; }
    public PointsOfInterestComponent Points { get; private set; }
    public IEntityDialogHost DialogHost { get; private set; }

    public IEntityMotor Motor { get; private set; }
    public IEntityFeedback Feedback { get; private set; }
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
    IEntityMotor motor, IEntityFeedback feedback,
    GameObject selfObject, EntityTuning tuning)
    {
        Motor = motor;
        Feedback = feedback;
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
        GetEntityComponent<MortalityComponent>().Died += () => (Feedback as IEntityDeathHandler)?.OnDeath();
        states = GetOrCreateEntityComponent<StatePatternComponent>();

        Vectorizer = new InputVectorizer(
            new R3.ReactiveProperty<float>(tuning.MaxHealth),
            new R3.ReactiveProperty<float>(tuning.MaxEnergy));
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

    public override void UpdateEntityCycle()
    {
        if (!IsActive.Value) return;
        if (Stats.Health.Value <= 0) return;

        timeOfDay = (timeOfDay + 1) % 24;

        float inDanger = ComputeDangerLevel();
        float timeToBreed = ComputeBreedReadiness();
        Perception.FindNearestEntity(Motor.Position, SelfObject, out float enemyDist);
        var food = Perception.FindNearestFood(Motor.Position);
        float foodDist = food != null ? Vector2.Distance(Motor.Position, food.transform.position) : -1f;

        LastInput = Vectorizer.Vectorize(
            Stats.Health.Value, Stats.Energy.Value,
            LastActionIndex, timeOfDay,
            inDanger, timeToBreed,
            Speech.OtherSpeech, enemyDist, foodDist);

        Speech.ConsumeOtherSpeech();

        bool isIdle = states.behaviourCurrent.GetType() == typeof(SurviveState)
              && LastActionIndex == (int)EntityAction.Idle;
        Stats.Tick(Time.deltaTime, Tuning.EnergyRegenRate, isIdle);
        if (Stats.Health.Value <= 0) return;

        int actionIndex = Brain.Predict(LastInput);
        LastActionIndex = actionIndex;

        var targetType = GoalStateMap[Brain.CurrentGoal];
        if (states.behaviourCurrent.GetType() != targetType)
        {
            states.SetBehaviourAsync(states.GetBehaviourByType(targetType)).Forget();
        }
        ((EntityActionState)states.behaviourCurrent).EnqueueAction((EntityAction)actionIndex);
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

    public override void DeepClean()
    {
        Vectorizer?.Dispose();
    }
}