using UnityEngine;

namespace TheRavine.EntityControl
{
    public class MobEntity : AEntity
    {
        [SerializeField] private GameObject view;
        [SerializeField] private bool isActive;
        [SerializeField] private Vector2 direction;
        public RoamMoveController moveController;
        public Animator animator;
        public override void SetUpEntityData(EntityInfo _entityInfo)
        {
            base.AddComponentToEntity(new MainComponent(_entityInfo.name, _entityInfo.prefab.GetInstanceID(), new EntityStats(_entityInfo.statsInfo)));
            moveController.SetInitialValues(this);
            // _entityGameData = new EntityGameData(_entityInfo);
            // crosshair.gameObject.SetActive(false);
        }
        public override Vector2 GetEntityPosition() => (Vector2)this.transform.position;
        public override Vector2 GetEntityVelocity()
        {
            return new Vector2();
        }
        public override void UpdateEntityCycle()
        {
        }
        public override void Init()
        {
            EnableView();
        }
        public override void EnableView()
        {
            isActive = true;
            moveController.EnableComponents();
            view.SetActive(isActive);
        }
        public override void DisableView()
        {
            isActive = false;
            moveController.DisableComponents();
            view.SetActive(isActive);
        }
    }
}