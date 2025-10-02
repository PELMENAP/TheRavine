using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Base;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(ShadowCreator))]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator defaultAnimator, shadowAnimator;
        
        private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
        private static readonly int VerticalHash = Animator.StringToHash("Vertical");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private bool shouldAnimateShadow;
        
        private ShadowCreator cachedShadowCreator;
        private GameSettings gameSettings;

        private void Awake()
        {
            defaultAnimator ??= GetComponent<Animator>();
            cachedShadowCreator = GetComponent<ShadowCreator>();
            gameSettings = ServiceLocator.GetService<SettingsModel>().GameSettings.CurrentValue;
        }
        
        public async UniTask SetUpAsync()
        {
            if (defaultAnimator == null)
            {
                Debug.LogError($"Default Animator is null on {gameObject.name}");
                return;
            }
            
            await InitializeShadowAnimatorAsync();
        }
        
        private async UniTask InitializeShadowAnimatorAsync()
        {
            if (cachedShadowCreator == null) return;
            
            await UniTask.Delay(100);
            
            var shadowObject = cachedShadowCreator.shadow;
            if (shadowObject != null &&
                shadowObject.TryGetComponent<Animator>(out var animator))
            {
                shadowAnimator = animator;
            }
            shouldAnimateShadow = gameSettings.enableShadows && shadowAnimator != null;
        }
        
        public void Animate(Vector2 movementDirection, float movementSpeed)
        {
            AnimateAnimator(defaultAnimator, movementDirection,  movementSpeed);
            
            if (shouldAnimateShadow)
            {
                AnimateAnimator(shadowAnimator, movementDirection,  movementSpeed);
            }
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
        public void RefreshShadowAnimationState()
        {
            shouldAnimateShadow = gameSettings.enableShadows && shadowAnimator != null;
        }
        private void OnDisable()
        {
            if (defaultAnimator != null)
            {
                defaultAnimator.SetFloat(SpeedHash, 0);
            }
            
            if (shadowAnimator != null)
            {
                shadowAnimator.SetFloat(SpeedHash, 0);
            }
        }
    }
}