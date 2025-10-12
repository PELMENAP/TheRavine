using Unity.Netcode;
using R3;

namespace TheRavine.EntityControl
{
    public abstract class AEntityViewModel : NetworkBehaviour
    {
        public AEntity Entity { get; private set; }

        public void Initialize(AEntity entity)
        {
            Entity = entity;
            SetupSubscriptions();
        }

        protected virtual void SetupSubscriptions()
        {
            Entity.IsActive.Subscribe(isActive =>
            {
                if(isActive) OnViewEnable();
                else OnViewDisable();
            });

            Entity.OnUpdate.Subscribe(_ => OnViewUpdate());
        }
        protected abstract void OnViewUpdate();
        protected abstract void OnViewDisable();
        protected abstract void OnViewEnable();
    }
}