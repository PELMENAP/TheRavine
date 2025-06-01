using UnityEngine;
using System;

using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.EntityControl
{
    public class PlayerEntity : AEntity
    {
        private IEntityController playerController;
        private StatePatternComponent statePatternComponent;
        private ILogger logger;
        public PlayerEntity(IEntityController controller, ILogger logger)
        {
            this.logger = logger;
            playerController = controller;
        }

        public void AddComponentsToEntity(EntityInfo entityInfo, AEntityViewModel aEntityModelView)
        {
            statePatternComponent = new StatePatternComponent();
            base.AddComponentToEntity(statePatternComponent);
            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.prefab.GetInstanceID(), new EntityStats(entityInfo.statsInfo)));
            base.AddComponentToEntity(new MovementComponent(new EntityMovementBaseStats(entityInfo.movementStatsInfo)));
            base.AddComponentToEntity(new AimComponent(new EntityAimBaseStats(entityInfo.aimStatsInfo)));
            base.AddComponentToEntity(new TransformComponent(aEntityModelView.transform, aEntityModelView.transform));
        }

        public override void Init()
        {
            playerController.SetInitialValues(this, logger);
            SetBehaviourIdle();
        }
        public override Vector2 GetEntityVelocity()
        {
            return new Vector2();
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
    }
}