using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Netcode;

namespace TheRavine.EntityControl
{
    public abstract class AEntity
    {
        public event Action OnActiveStateChanged;
        public bool IsAlive { get; private set; } = true;
        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnActiveStateChanged?.Invoke();
                }
            }
        }
        private Dictionary<Type, IComponent> _components = new();
        public void AddComponentToEntity(IComponent component)
        {
            _components[component.GetType()] = component;
        }
        public T GetEntityComponent<T>() where T : IComponent
        {
            _components.TryGetValue(typeof(T), out IComponent component);
            return (T)component;
        }
        public void Delete()
        {
            IsAlive = false;
            foreach (var component in _components.Values)
                component.Dispose();
            _components.Clear();
        }
        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;

        public abstract void SetUpEntityData(EntityInfo entityInfo, IEntityController controller);
        public abstract void Init(Action onUpdateAction);
        public abstract void UpdateEntityCycle();
        public abstract Vector2 GetEntityVelocity();
    }
}