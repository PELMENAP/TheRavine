using UnityEngine;
using System.Collections.Generic;
using System;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    private static readonly List<Transform> _playersTransforms = new();
    public static bool Register<T>(T service) where T : MonoBehaviour
    {
        return RegisterInternal(typeof(T), service);
    }

    public static bool RegisterService<T>(T service) where T : class
    {
        return RegisterInternal(typeof(T), service);
    }

    private static bool RegisterInternal(Type type, object service)
    {
        if (!_services.ContainsKey(type) && service != null)
        {
            _services[type] = service;
            return true;
        }
        return false;
    }
    public static void RegisterPlayer<T>(T player) where T : MonoBehaviour
    {
        if (player != null)
            _playersTransforms.Add(player.transform);
    }
    public static T GetMonoService<T>() where T : MonoBehaviour
    {
        if (_services.TryGetValue(typeof(T), out var svc) && svc is T mb)
            return mb;

        LogWarning(typeof(T));
        return null;
    }

    public static bool TryGetMonoService<T>(out T service) where T : MonoBehaviour
    {
        if (_services.TryGetValue(typeof(T), out var svc) && svc is T mb)
        {
            service = mb;
            return true;
        }
        service = null;
        return false;
    }

    public static T GetService<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var svc) && svc is T s)
            return s;

        LogWarning(typeof(T));
        throw new InvalidOperationException($"Сервис {typeof(T).Name} не зарегистрирован");
    }

    public static bool TryGetService<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var svc) && svc is T s)
        {
            service = s;
            return true;
        }
        service = null;
        return false;
    }
    public static Transform GetPlayerTransform()
    {
        if (_playersTransforms.Count == 0)
        {
            GetOptional<ILogger>()?.LogError("Нет зарегистрированных игроков");
            return null;
        }
        return _playersTransforms[0];
    }

    public static List<Transform> GetPlayersTransforms() => _playersTransforms;
    private static void LogWarning(Type type)
    {
        GetOptional<ILogger>()?.LogWarning($"Сервис {type.Name} не зарегистрирован");
    }

    private static T GetOptional<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var svc) && svc is T s)
            return s;
        return null;
    }

    public static void Clear()
    {
        foreach (var obj in _services.Values)
        {
            if (obj is IDisposable d)
                d.Dispose();
        }
        _services.Clear();
        _playersTransforms.Clear();
    }
}