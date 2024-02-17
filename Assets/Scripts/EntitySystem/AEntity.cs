using TheRavine.EntityControl;
using UnityEngine;
using System.Collections.Generic;
public abstract class AEntity : MonoBehaviour
{
    private Dictionary<System.Type, IComponent> _components = new Dictionary<System.Type, IComponent>();
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
        _components.Clear();
    }

    public abstract void SetUpEntityData(EntityInfo entityInfo);
    public abstract void Init();
    public abstract void UpdateEntityCycle();
    public abstract Vector2 GetEntityPosition();
    public abstract void EnableView();
    public abstract void DisableView();
}