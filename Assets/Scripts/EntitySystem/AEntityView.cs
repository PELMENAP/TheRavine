using UnityEngine;

namespace TheRavine.EntityControl
{
    public abstract class AEntityView : MonoBehaviour
    {
        protected AEntityViewModel ViewModel { get; private set; }
        
        public void Initialize(AEntityViewModel viewModel)
        {
            ViewModel = viewModel;
            SetupBindings();
        }
        
        protected abstract void SetupBindings();
    }
}