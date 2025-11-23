using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.EntityControl
{
    public class PlayerEntity : AEntity
    {
        private StatePatternComponent statePatternComponent;
        private IEntityController PlayerController;
        private readonly IRavineLogger logger;
        public PlayerEntity(IRavineLogger logger)
        {
            this.logger = logger;
        }

        public void AddComponentsToEntity(EntityInfo entityInfo, AEntityViewModel aEntityModelView, IEntityController entityController)
        {
            statePatternComponent = new StatePatternComponent();
            PlayerController = entityController;
            base.AddComponentToEntity(statePatternComponent);
            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            base.AddComponentToEntity(new EnergyComponent(entityInfo.EnergyInfo));
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.Prefab.GetInstanceID()));
            base.AddComponentToEntity(new MovementComponent(entityInfo.MovementInfo, entityController));
            base.AddComponentToEntity(new AimComponent(entityInfo.AimStatsInfo));
            base.AddComponentToEntity(new TransformComponent(aEntityModelView.transform, aEntityModelView.transform));
        }

        public override void Init()
        {
            PlayerController.SetInitialValues(this, logger);
            SetBehaviourIdle();
        }
        public override void UpdateEntityCycle()
        {
            if (!base.IsActive.Value) return;
            if (statePatternComponent.behaviourCurrent == null) return;
                statePatternComponent.behaviourCurrent.Update();
            base.OnUpdate.Execute(Unit.Default);
        }

        public void SetBehaviourIdle()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<PlayerBehaviourIdle>()).Forget();
            PlayerController.EnableComponents();
        }

        public void SetBehaviourDialog()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<PlayerBehaviourDialogue>()).Forget();
            PlayerController.SetZeroValues();
            PlayerController.DisableComponents();
        }

        public void SetBehaviourSit()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<PlayerBehaviourSit>()).Forget();
            PlayerController.SetZeroValues();
            PlayerController.DisableComponents();
        }
    }
}