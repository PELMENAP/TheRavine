using Unity.Netcode;
using UnityEngine;
using R3;
using System;

namespace TheRavine.EntityControl
{
    public class PlayerView : AEntityView
    {
        [SerializeField] private Animator animator, shadowAnimator;
        [SerializeField] private Transform crosshair;
        [SerializeField] private Transform playerMark;
        
        protected override void SetupBindings()
        {
            // base.SetupBindings();
            
            // ViewModel.MovementDirection
            //     .Subscribe(direction => UpdateAnimator(direction))
            //     .AddTo(Disposables);
                
            // ViewModel.MovementSpeed
            //     .Subscribe(speed => {
            //         animator.SetFloat("Speed", speed);
            //         if (Settings.isShadow) shadowAnimator.SetFloat("Speed", speed);
            //     })
            //     .AddTo(Disposables);
        }
        
        private void UpdateAnimator(Vector2 direction)
        {
            // if (direction != Vector2.zero)
            // {
            //     animator.SetFloat("Horizontal", direction.x);
            //     animator.SetFloat("Vertical", direction.y);
            //     if (Settings.isShadow)
            //     {
            //         shadowAnimator.SetFloat("Horizontal", direction.x);
            //         shadowAnimator.SetFloat("Vertical", direction.y);
            //     }
            // }
            // animator.SetFloat("Speed", movementSpeed);
            // if (Settings.isShadow) shadowAnimator.SetFloat("Speed", movementSpeed);
        }
    }
}