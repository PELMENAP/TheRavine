namespace TheRavine.EntityControl
{
    public abstract class AEntity : UnityEngine.MonoBehaviour
    {
        private System.Collections.Generic.Dictionary<System.Type, IComponent> _components = new System.Collections.Generic.Dictionary<System.Type, IComponent>();
        public void AddComponentToEntity(IComponent component)
        {
            _components[component.GetType()] = component;
        }
        public T GetEntityComponent<T>() where T : IComponent
        {
            _components.TryGetValue(typeof(T), out IComponent component);
            return (T)component;
        }
        public void BreakUpEntity()
        {
            foreach (var item in _components)
                item.Value.Dispose();
            _components.Clear();
        }

        public abstract void SetUpEntityData(EntityInfo entityInfo);
        public abstract void Init();
        public abstract void UpdateEntityCycle();
        public abstract UnityEngine.Vector2 GetEntityPosition();
        public abstract UnityEngine.Vector2 GetEntityVelocity();
        public abstract void EnableView();
        public abstract void DisableView();
    }
}