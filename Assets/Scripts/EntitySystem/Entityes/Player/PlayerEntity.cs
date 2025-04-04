using UnityEngine;
using System;

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
            playerController.SetInitialValues(this, logger);
        }

        public void AddComponentsToEntity(EntityInfo entityInfo, AEntityModelView aEntityModelView)
        {
            base.AddComponentToEntity(new StatePatternComponent());
            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.prefab.GetInstanceID(), new EntityStats(entityInfo.statsInfo)));
            base.AddComponentToEntity(new MovementComponent(new EntityMovementBaseStats(entityInfo.movementStatsInfo)));
            base.AddComponentToEntity(new AimComponent(new EntityAimBaseStats(entityInfo.aimStatsInfo)));
            base.AddComponentToEntity(new TransformComponent(aEntityModelView.transform, aEntityModelView.transform));
        }

        public override void Init()
        {
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
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourIdle>());
            playerController.EnableComponents();
        }

        public void SetBehaviourDialog()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourDialogue>());
            playerController.SetZeroValues();
            playerController.DisableComponents();
        }

        public void SetBehaviourSit()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourSit>());
            playerController.SetZeroValues();
            playerController.DisableComponents();
        }
    }
}