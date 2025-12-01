using System;
using System.Collections.Generic;
using R3;

public class ServiceContainer : IServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();
    private readonly Dictionary<Type, Subject<object>> _serviceSubjects = new();

    public Observable<T> OnServiceRegisteredAs<T>() where T : class
    {
        if (TryGet<T>(out var existing))
            return Observable.Return(existing);

        var type = typeof(T);
        if (!_serviceSubjects.TryGetValue(type, out var subject))
        {
            subject = new Subject<object>();
            _serviceSubjects[type] = subject;
        }

        return subject.Select(s => (T)s);
    }

    public bool Register<T>(T service) where T : class
    {
        if (service == null || _services.ContainsKey(typeof(T)))
            return false;

        var type = typeof(T);
        _services[type] = service;
        
        if (_serviceSubjects.TryGetValue(type, out var subject))
            subject.OnNext(service);

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
        
        foreach (var subject in _serviceSubjects.Values)
            subject.Dispose();
        
        _services.Clear();
        _serviceSubjects.Clear();
    }
}