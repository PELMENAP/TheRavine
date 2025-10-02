using System;
using System.Collections.Generic;

public class ServiceContainer : IServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    public bool Register<T>(T service) where T : class
    {
        if (service == null || _services.ContainsKey(typeof(T)))
            return false;
        _services[typeof(T)] = service;
        return true;
    }

    public bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var s) && s is T casted)
        {
            service = casted;
            return true;
        }
        service = null;
        return false;
    }

    public T Get<T>() where T : class
    {
        if (TryGet<T>(out var service))
            return service;
        throw new InvalidOperationException($"Сервис {typeof(T).Name} не зарегистрирован");
    }

    public void Clear()
    {
        foreach (var s in _services.Values)
            if (s is IDisposable d) d.Dispose();
        _services.Clear();
    }
}
