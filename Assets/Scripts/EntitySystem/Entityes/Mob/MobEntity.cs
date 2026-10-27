using UnityEngine;
using System;

namespace TheRavine.EntityControl
{
    public class MobEntity : AEntity
    {
        [SerializeField] private Vector2 direction;
        private readonly RoamMoveController moveController;
        [SerializeField] private Animator animator;
        public MobEntity(EntityInfo entityInfo, RavineLogger logger)
        {
            base.AddComponentToEntity(new MainComponent(entityInfo.name, entityInfo.Prefab.GetInstanceID(), 0));
            moveController.SetInitialValues(this, logger);
        }
        public override void SetUp()
        {
            throw new NotImplementedException();
        }
        public override void Init()
        {
            base.Activate();
        }
        public override void UpdateEntityCycle()
        {
        }
    }
}