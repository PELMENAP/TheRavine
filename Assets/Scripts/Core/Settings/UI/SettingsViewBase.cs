using UnityEngine;
using R3;

namespace TheRavine.Base
{
    public abstract class SettingsViewBase<TSettings> : MonoBehaviour 
        where TSettings : class, new()
    {
        protected CompositeDisposable Disposables { get; } = new();
        protected SettingsMediator Mediator { get; private set; }

        protected virtual void Start()
        {
            Mediator = ServiceLocator.GetService<SettingsMediator>();
            
            InitializeControls();
            BindToModel();
        }

        protected abstract void InitializeControls();
        protected abstract void BindToModel();
        protected abstract void UpdateView(TSettings settings);

        protected void OnDestroy() => Disposables?.Dispose();
    }
}