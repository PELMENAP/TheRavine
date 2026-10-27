using Unity.Netcode;
using R3;

namespace TheRavine.EntityControl
{
    public abstract class AEntityViewModel : NetworkBehaviour
    {
        public IEntity Entity { get; private set; }

        protected DisposableBag Subscriptions;

        public void Initialize(IEntity entity)
        {
            Subscriptions.Clear();
            Entity = entity;
            SetupSubscriptions();
        }

        protected virtual void SetupSubscriptions()
        {
            Entity.IsActive.Subscribe(isActive =>
            {
                if(isActive) OnViewEnable();
                else OnViewDisable();
            }).AddTo(ref Subscriptions);

            Entity.OnUpdate.Subscribe(_ => OnViewUpdate()).AddTo(ref Subscriptions);
        }

        public override void OnNetworkDespawn()
        {
            Subscriptions.Clear();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            Subscriptions.Dispose();
            base.OnDestroy();
        }
        protected abstract void OnViewUpdate();
        protected abstract void OnViewDisable();
        protected abstract void OnViewEnable();
    }
}