using UnityEngine;
using System;

namespace TheRavine.EntityControl
{
    public class MobEntity : AEntity
    {
        [SerializeField] private Vector2 direction;
        private IMobControllable moveController;
        [SerializeField] private Animator animator;
        public MobEntity(EntityInfo entityInfo)
        {
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.prefab.GetInstanceID(), new EntityStats(entityInfo.statsInfo)));
            moveController.SetInitialValues(this);
            // _entityGameData = new EntityGameData(_entityInfo);
            // crosshair.gameObject.SetActive(false);
        }
        public override void Init(Action onUpdateAction, IEntityController controller)
        {
            moveController = controller as IMobControllable;
            base.Activate();
        }
        public override Vector2 GetEntityVelocity() => moveController.GetEntityVelocity();
        public override void UpdateEntityCycle()
        {
            // if (isAlife)
            //     moveController.UpdateMobControllerCycle();
        }
        // public override void EnableView()
        // {
        //     Activate();
        //     moveController.EnableComponents();
        // }
        // public override void DisableView()
        // {
        //     Deactivate();
        //     moveController.DisableComponents();
        // }
    }
}