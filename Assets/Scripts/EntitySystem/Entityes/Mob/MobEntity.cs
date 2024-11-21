using UnityEngine;
namespace TheRavine.EntityControl
{
    public class MobEntity : AEntity
    {
        [SerializeField] private Vector2 direction;
        private IMobControllable moveController;
        [SerializeField] private Animator animator;
        public override void SetUpEntityData(EntityInfo _entityInfo)
        {
            moveController = this.GetComponent<IMobControllable>();
            base.AddComponentToEntity(new MainComponent(_entityInfo.name, _entityInfo.prefab.GetInstanceID(), new EntityStats(_entityInfo.statsInfo)));
            moveController.SetInitialValues(this);
            // _entityGameData = new EntityGameData(_entityInfo);
            // crosshair.gameObject.SetActive(false);
        }
        public override Vector2 GetEntityPosition() => (Vector2)this.transform.position;
        public override Vector2 GetEntityVelocity() => moveController.GetEntityVelocity();
        public override Transform GetModelTransform() => moveController.GetModelTransform();
        public override void UpdateEntityCycle()
        {
            // if (isAlife)
            //     moveController.UpdateMobControllerCycle();
        }
        public override void Init()
        {
            EnableView();
        }
        public override void EnableView()
        {
            Activate();
            moveController.EnableComponents();
        }
        public override void DisableView()
        {
            Deactivate();
            moveController.DisableComponents();
        }
    }
}