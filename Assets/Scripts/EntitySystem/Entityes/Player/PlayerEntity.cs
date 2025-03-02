using UnityEngine;
using System;

namespace TheRavine.EntityControl
{
    public class PlayerEntity : AEntity
    {
        private IEntityController playerController;
        private StatePatternComponent statePatternComponent;
        private Action onUpdateAction;
        private ILogger logger;
        public PlayerEntity(EntityInfo entityInfo, ILogger logger)
        {
            this.logger = logger;
            statePatternComponent = new StatePatternComponent();
            base.AddComponentToEntity(statePatternComponent);
            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.prefab.GetInstanceID(), new EntityStats(entityInfo.statsInfo)));
            base.AddComponentToEntity(new MovementComponent(new EntityMovementBaseStats(entityInfo.movementStatsInfo)));
            base.AddComponentToEntity(new AimComponent(new EntityAimBaseStats(entityInfo.aimStatsInfo)));
        }
        public override void Init(Action onUpdateAction, IEntityController controller)
        {
            playerController = controller;
            this.onUpdateAction = onUpdateAction;
            playerController.SetInitialValues(this, logger);
            SetBehaviourIdle();
        }
        public override Vector2 GetEntityVelocity()
        {
            return new Vector2();
        }
        public override void UpdateEntityCycle()
        {
            if (!base.IsAlive) return;
            if (statePatternComponent.behaviourCurrent == null) return;
                statePatternComponent.behaviourCurrent.Update();
            onUpdateAction?.Invoke();
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