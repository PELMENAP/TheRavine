using R3;
using System.Collections.Generic;
using System;

using UnityEngine;

namespace TheRavine.EntityControl
{
    public abstract class AEntity : IDisposable
    {
        public ReactiveCommand<Unit> OnUpdate { get; } = new();
        public ReactiveProperty<bool> IsActive { get; } = new ReactiveProperty<bool>(true);
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
        public T GetOrCreateEntityComponent<T>() where T : IComponent, new()
        {
            if (!_components.TryGetValue(typeof(T), out var component))
            {
                component = new T();
                _components.Add(typeof(T), component);
            }
            return (T)component;
        }
        public void Activate() => IsActive.Value = true;
        public void Deactivate() => IsActive.Value = false;

        public void Dispose()
        {
            foreach (var component in _components.Values)
                component.Dispose();
            _components.Clear();
            IsActive.Dispose();
        }

        public abstract void Init();
        public abstract void UpdateEntityCycle();
        public abstract Vector2 GetEntityVelocity();
    }
}