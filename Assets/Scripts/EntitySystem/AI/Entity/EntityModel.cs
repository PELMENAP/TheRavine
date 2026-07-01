using Cysharp.Threading.Tasks;

using TheRavine.EntityControl;
public class EntityModel : AEntity
{
    public StatsComponent Stats { get; private set; }
    public PerceptionComponent Perception { get; private set; }
    public BrainComponent Brain { get; private set; }
    public SpeechComponent Speech { get; private set; }
    private StatePatternComponent states;
    private InputVectorizer vectorizer;
    private IEntityMotor motor;

    private int lastActionIndex;
    private int timeOfDay;

    public void Configure(SharedHierarchicalBrain brain, EntityBrainContext ctx,
        IEntityMotor _motor, EntityTuning tuning)
    {
        Stats = GetOrCreateEntityComponent<StatsComponent>();
        Perception = GetOrCreateEntityComponent<PerceptionComponent>();
        AddComponentToEntity(new BrainComponent(brain, ctx));
        Brain = GetEntityComponent<BrainComponent>();
        Speech = GetOrCreateEntityComponent<SpeechComponent>();
        states = GetOrCreateEntityComponent<StatePatternComponent>();
        motor = _motor;
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
        var input = BuildInput();
        int actionIndex = Brain.Predict(input);
        lastActionIndex = actionIndex;

        var targetStateType = GoalToStateType(Brain.CurrentGoal);
        if (states.behaviourCurrent.GetType() != targetStateType)
            states.SetBehaviourAsync((AState)states.GetType()
                .GetMethod(nameof(StatePatternComponent.GetBehaviour))
                .MakeGenericMethod(targetStateType).Invoke(states, null)).Forget();

        ((EntityActionState)states.behaviourCurrent).EnqueueAction((EntityAction)actionIndex);
        OnUpdate.OnNext(Unit.Default);
    }
}