using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Base;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator defaultAnimator;
        
        private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
        private static readonly int VerticalHash = Animator.StringToHash("Vertical");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private GameSettings gameSettings;

        private void Start()
        {
            defaultAnimator ??= GetComponent<Animator>();
            gameSettings = ServiceLocator.GetService<SettingsModel>().GameSettings.CurrentValue;
        }
        
        public async UniTask SetUpAsync()
        {
            if (defaultAnimator == null)
            {
                Debug.LogError($"Default Animator is null on {gameObject.name}");
                return;
            }
            await UniTask.CompletedTask;
        }
        public void Animate(Vector2 movementDirection, float movementSpeed)
        {
            AnimateAnimator(defaultAnimator, movementDirection,  movementSpeed);
        }
        private void AnimateAnimator(Animator animator, Vector2 movementDirection, float movementSpeed)
        {
            if (animator == null) return;
            
            if (movementDirection != Vector2.zero)
            {
                animator.SetFloat(HorizontalHash, movementDirection.x);
                animator.SetFloat(VerticalHash, movementDirection.y);
            }
            
            animator.SetFloat(SpeedHash, movementSpeed);
        }
        private void OnDisable()
        {
            if (defaultAnimator != null)
            {
                defaultAnimator.SetFloat(SpeedHash, 0);
            }
        }
    }
}