using Cysharp.Threading.Tasks;
using R3;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.EntityControl.Virology;
using TheRavine.Generator;

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
        private TransformComponent transformComponent;
        private MapGenerator map;
        private float immuneUpkeepMul = 1f;
        private PlayerSkillGraph skills;
        private readonly PlayerStress stress = new();
        private EntitySpatialGrid grid;
        private float baseMaxHealth;
        private float baseMaxEnergy;
        private double lastCycleTime;
        private int windStepSeen = -1;
        private int lightningStepSeen = -1;
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
            AddCreatureComponents(entityInfo.EnergyInfo, aEntityModelView is Unity.Netcode.NetworkBehaviour network && network.IsServer && network.IsOwner);
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.Prefab.GetInstanceID(), clientId));
            base.AddComponentToEntity(new MovementComponent(entityInfo.MovementInfo));
            base.AddComponentToEntity(new AimComponent(entityInfo.AimStatsInfo));
            transformComponent = new TransformComponent(aEntityModelView.transform, aEntityModelView.transform);
            base.AddComponentToEntity(transformComponent);
            this.clientId = clientId;
        }

        private void AddCreatureComponents(EnergyInfo energyInfo, bool savesSkills)
        {
            var rules = SimulationRules.Active;
            var genes = GeneticParameters.Default;

            PlayerSkillSave saved = null;
            if (savesSkills && ServiceLocator.Services.TryGet(out WorldRegistry registry) && registry.HasLoadedWorld)
                saved = registry.GetCurrentState().playerSkills;
            skills = PlayerSkillGraph.Create(SkillAtlas.Active, ref genes, saved, WorldAuthority.IsAuthority);
            baseMaxHealth = rules.BaseMaxHealth;
            baseMaxEnergy = energyInfo.MaxEnergy;

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
            base.AddComponentToEntity(skills);
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
            if (WorldAuthority.IsAuthority)
            {
                TickMetabolism();
                skills.Tick(SimulationClock.Time);
            }
            RefreshSkillEffects();
            if (playerController.IsOwner && statePatternComponent.behaviourCurrent != null)
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

            float environmentMul = CreatureCore.EnvironmentDrainMul(0f, night, mods.Fever, winter, in rules);
            StepStress(dt, environmentMul, night || winter, in rules);
            float basalMul = environmentMul * CreatureCore.StressDrainMul(stress.Effect, in rules) * immuneUpkeepMul;
            CreatureCore.Metabolize(stats, mortality, dt, 0f, mods.MetabolismMultiplier, basalMul,
                in rules, out float metabolismCredit);
            virology.CreditDurableEffects(0f, metabolismCredit, stats.MaxEnergy);
            float burn = BurnDamage(dt);
            if (burn > 0f)
            {
                mortality.NoteHarm(DeathCause.Burn, stats.Hp - burn);
                stats.Hp -= burn;
            }
            float gust = WindDamage(dt);
            if (gust > 0f)
            {
                mortality.NoteHarm(DeathCause.Wind, stats.Hp - gust);
                stats.Hp -= gust;
            }
            float bolt = LightningDamage();
            if (bolt > 0f)
            {
                mortality.NoteHarm(DeathCause.Lightning, stats.Hp - bolt);
                stats.Hp -= bolt;
            }
            stats.Commit();
            energy.Sync();
        }

        private void StepStress(float dt, float environmentMul, bool exposed, in SimulationRules.RulesFrame rules)
        {
            int alarmed = 0, near = 0;
            if (rules.StressSourcesEnabled && rules.PlayerStressEnabled && rules.StressContagionEnabled && transformComponent != null
                && (grid != null || ServiceLocator.Services.TryGet(out grid)))
                alarmed = grid.CountAlarmed(transformComponent.GetEntityPosition(), rules.StressContagionRadius, out near);
            float energyFraction = stats.MaxEnergy > 0f ? stats.En / stats.MaxEnergy : 1f;
            stress.Step(dt, energyFraction, environmentMul, exposed, alarmed, near, in rules);
        }

        public PlayerStress Stress => stress;

        private void RefreshSkillEffects()
        {
            skills.Refresh();
            if (!skills.ConsumeModifiersChanged() || stats == null || stats.IsDisposed) return;

            ref readonly var mods = ref skills.Modifiers;
            stats.Rescale(baseMaxHealth * mods.Health, baseMaxEnergy * mods.EnergyCapacity);
            energy.Sync();
        }

        public PlayerSkillGraph SkillGraph => skills;

        public void AwardSkillXp(EntityAction action) => skills?.Award(action, SimulationClock.Time);

        public void CaptureSkills(WorldRegistry registry)
        {
            if (skills == null || !skills.IsAuthority) return;
            registry.UpdateState(s =>
            {
                s.playerSkills ??= new PlayerSkillSave();
                skills.Capture(s.playerSkills, SimulationClock.Time);
            });
        }

        private float BurnDamage(float dt)
        {
            if (map == null && !ServiceLocator.Services.TryGet(out map)) return 0f;
            FireEvent fire = map.Events?.Fire;
            if (fire == null || fire.BurningCount == 0 || transformComponent == null) return 0f;

            var p = transformComponent.GetEntityPosition();
            return fire.HeatDamageAt(new float2(p.x, p.z)) * dt;
        }

        private float WindDamage(float dt)
        {
            if (map == null && !ServiceLocator.Services.TryGet(out map)) return 0f;
            WildWindEvent wind = map.Events?.Wind;
            if (wind == null || wind.IsDisposed || !wind.Active || transformComponent == null) return 0f;

            float3 p = transformComponent.GetEntityPosition();
            if (!wind.InBand(p.y)) return 0f;
            if (wind.StepIndex != windStepSeen)
            {
                windStepSeen = wind.StepIndex;
                wind.ReportCreature(p);
            }
            return wind.DamageAt(p) * dt;
        }

        private float LightningDamage()
        {
            if (map == null && !ServiceLocator.Services.TryGet(out map)) return 0f;
            WeatherEvent weather = map.Events?.Weather;
            if (weather == null || weather.IsDisposed || transformComponent == null) return 0f;
            if (weather.StepIndex == lightningStepSeen) return 0f;
            lightningStepSeen = weather.StepIndex;

            float3 p = transformComponent.GetEntityPosition();
            return weather.LightningDamageAt(p.xz);
        }

        private void OnDied()
            => RavineLog.Warning($"Player {clientId} died: {mortality.Cause}");

        public bool RequestPlace(long position, int objectId, byte level = 0) => playerController.RequestPlace(position, objectId, level);
        public bool RequestPickUp(long position, byte level = 0) => playerController.RequestPickUp(position, level);
        public bool RequestCraft(int recipeIndex) => playerController.RequestCraft(recipeIndex);

        public void ApplyServerVitals(float health, float energyValue)
        {
            if (stats == null || stats.IsDisposed) return;
            stats.En = energyValue;
            stats.Hp = health;
            energy.Sync();
        }

        public void SetBehaviourIdle()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<PlayerBehaviourIdle>()).Forget();
            playerController.Resume();
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
            playerController.Suspend();
            playerController.DisableComponents();
        }

        public override void DeepClean()
        {
            ServiceLocator.Players.UnregisterPlayer(this);
        }
    }
}