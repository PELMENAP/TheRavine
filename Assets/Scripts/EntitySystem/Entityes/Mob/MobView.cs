using UnityEngine;
using Unity.Netcode;

namespace TheRavine.EntityControl
{
    public class MobView : NetworkBehaviour
    {
        private AEntity mobEntity;
        [SerializeField] private Vector2 direction;
        private IEntityController moveController;
        [SerializeField] private Animator animator;

        private void Awake() {
            // mobEntity = new MobEntity();
            mobEntity.AddComponentToEntity(new TransformComponent(this.transform, moveController.GetModelTransform()));
        }
        
        public void EnableView()
        {
            // mobEntity.Activate();
            moveController.EnableComponents();
        }
        public void DisableView()
        {
            // Deactivate();
            moveController.DisableComponents();
        }
    }
}