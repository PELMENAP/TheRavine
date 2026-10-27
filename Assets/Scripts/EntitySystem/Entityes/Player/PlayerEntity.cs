using Cysharp.Threading.Tasks;
using R3;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.EntityControl.Virology;

namespace TheRavine.EntityControl
{
    public class PlayerEntity : AEntity
    {
        private StatePatternComponent statePatternComponent;
        private PlayerController playerController;
        private StatsComponent stats;
        private MortalityComponent mortality;
        private VirologyComponent virology;
        private EnergyComponent energy;
        private DayCycle dayCycle;
        private float immuneUpkeepMul = 1f;
        private double lastCycleTime;
        private ulong clientId;
        private readonly RavineLogger logger;
        public PlayerEntity(RavineLogger logger)
        {
            this.logger = logger;
        }

        public void AddComponentsToEntity(EntityInfo entityInfo, AEntityViewModel aEntityModelView, ulong clientId)
        {
            statePatternComponent = new StatePatternComponent();
            playerController = aEntityModelView.gameObject.GetComponent<PlayerController>();;
            base.AddComponentToEntity(statePatternComponent);
            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            AddCreatureComponents(entityInfo.EnergyInfo);
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.Prefab.GetInstanceID(), clientId));
            base.AddComponentToEntity(new MovementComponent(entityInfo.MovementInfo));
            base.AddComponentToEntity(new AimComponent(entityInfo.AimStatsInfo));
            base.AddComponentToEntity(new TransformComponent(aEntityModelView.transform, aEntityModelView.transform));
            this.clientId = clientId;
        }

        private void AddCreatureComponents(EnergyInfo energyInfo)
        {
            var rules = SimulationRules.Active;
            var genes = GeneticParameters.Default;

            stats = new StatsComponent();
            stats.FillComponent(rules.BaseMaxHealth, energyInfo.MaxEnergy);
            stats.En = math.clamp(energyInfo.Energy, 0f, stats.MaxEnergy);
            energy = new EnergyComponent(stats);
            mortality = new MortalityComponent(stats);
            mortality.Died += OnDied;
            virology = CreatureCore.CreateVirology(in genes, null, rules);
            immuneUpkeepMul = CreatureCore.ImmuneUpkeepMul(in genes, rules);
            lastCycleTime = SimulationClock.TimeD;

            base.AddComponentToEntity(stats);
            base.AddComponentToEntity(energy);
            base.AddComponentToEntity(mortality);
            base.AddComponentToEntity(virology);
        }

        public void AddWorldComponents(WorldRegistry worldRegistry)
        {
            base.AddComponentToEntity(new CurrencyComponent(worldRegistry.GetCurrentState().currency));
        }

        public override void Init()
        {
            playerController.SetInitialValues(this, logger);
        }

        public override void SetUp()
        {
            playerController.SetUp();
            SetBehaviourIdle();
        }
        public override void UpdateEntityCycle()
        {
            if (!base.IsActive.Value) return;
            TickMetabolism();
            if (statePatternComponent.behaviourCurrent == null) return;
                statePatternComponent.behaviourCurrent.Update();
            base.OnUpdate.Execute(Unit.Default);
        }

        private void TickMetabolism()
        {
            if (stats == null || stats.IsDisposed || mortality.IsDead) return;

            ref readonly var rules = ref SimulationRules.Frame;
            double now = SimulationClock.TimeD;
            float dt = math.min((float)(now - lastCycleTime), rules.MaxCycleDt);
            if (dt <= 0f) return;
            lastCycleTime = now;

            bool night  = CreatureCore.IsNightPhase(CreatureCore.DayPhase(ref dayCycle), in rules);
            bool winter = SeasonClock.IsWinter((float)now);

            stats.Open();
            var host = new HostState
            {
                HpFraction     = stats.Hp / stats.MaxHealth,
                Night          = night,
                Crowd          = 0,
                WeakThreshold  = rules.SkipWeakHpFraction,
                CrowdThreshold = rules.SkipCrowdCount,
                AtNest         = false,
                HomeCrowd      = rules.HomeCompulsionCrowd,
            };
            CreatureCore.StepVirology(virology, stats, mortality, dt, in host);
            ref var mods = ref virology.Modifiers;

            float basalMul = CreatureCore.EnvironmentDrainMul(0f, night, mods.Fever, winter, in rules) * immuneUpkeepMul;
            CreatureCore.Metabolize(stats, mortality, dt, 0f, mods.MetabolismMultiplier, basalMul,
                in rules, out float metabolismCredit);
            virology.CreditDurableEffects(0f, metabolismCredit, stats.MaxEnergy);
            stats.Commit();
            energy.Sync();
        }

        private void OnDied()
            => RavineLog.Warning($"Player {clientId} died: {mortality.Cause}");

        public bool RequestPlace(long position, int objectId) => playerController.RequestPlace(position, objectId);
        public bool RequestPickUp(long position) => playerController.RequestPickUp(position);

        public void SetBehaviourIdle()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<PlayerBehaviourIdle>()).Forget();
            playerController.EnableComponents();
        }

        public void SetBehaviourDialog()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<PlayerBehaviourDialogue>()).Forget();
            playerController.SetZeroValues();
            playerController.DisableComponents();
        }

        public void SetBehaviourSit()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<PlayerBehaviourSit>()).Forget();
            playerController.SetZeroValues();
            playerController.DisableComponents();
        }

        public override void DeepClean()
        {
        }
    }
}