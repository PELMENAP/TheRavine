using UnityEngine;
using System;

namespace TheRavine.EntityControl
{
    public class MobEntity : AEntity
    {
        [SerializeField] private Vector2 direction;
        private IEntityController moveController;
        [SerializeField] private Animator animator;
        public MobEntity(EntityInfo entityInfo, IRavineLogger logger)
        {
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.Prefab.GetInstanceID()));
            moveController.SetInitialValues(this, logger);
            // _entityGameData = new EntityGameData(_entityInfo);
            // crosshair.gameObject.SetActive(false);
        }
        public override void Init()
        {
            base.Activate();
        }
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