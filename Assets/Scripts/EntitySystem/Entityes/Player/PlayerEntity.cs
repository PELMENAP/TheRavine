using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.EntityControl
{
    public class PlayerEntity : AEntity
    {
        private StatePatternComponent statePatternComponent;
        private PlayerController playerController;
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
            base.AddComponentToEntity(new EnergyComponent(entityInfo.EnergyInfo));
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.Prefab.GetInstanceID(), clientId));
            base.AddComponentToEntity(new MovementComponent(entityInfo.MovementInfo));
            base.AddComponentToEntity(new AimComponent(entityInfo.AimStatsInfo));
            base.AddComponentToEntity(new TransformComponent(aEntityModelView.transform, aEntityModelView.transform));
        }

        public override void Init()
        {
            playerController.SetInitialValues(this, logger);
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
            playerController.Delete();
        }
    }
}