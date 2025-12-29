using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

public class ServiceContainer
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
        if (service == null)
        {
            Debug.LogError($"Попытка зарегистрировать null сервис типа {typeof(T).Name}");
            return false;
        }

        var type = typeof(T);
        
        if (_services.ContainsKey(type))
        {
            Debug.LogWarning($"Сервис {type.Name} уже зарегистрирован. Повторная регистрация игнорируется.");
            return false;
        }

        _services[type] = service;
        
        if (!_serviceSubjects.TryGetValue(type, out var subject))
        {
            subject = new Subject<object>();
            _serviceSubjects[type] = subject;
        }
        
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
        
        var type = typeof(T);
        var availableServices = string.Join(", ", _services.Keys);
        throw new InvalidOperationException(
            $"Сервис {type.Name} не зарегистрирован.\n" +
            $"Доступные сервисы: [{availableServices}]\n" +
            $"Проверьте порядок инициализации и убедитесь, что типы совпадают при Register<> и Get<>.");
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
    public void LogRegisteredServices()
    {
        Debug.Log("=== Зарегистрированные сервисы ===");
        foreach (var kvp in _services)
        {
            Debug.Log($"  • {kvp.Key.Name} -> {kvp.Value?.GetType().Name ?? "null"}");
        }
    }
}