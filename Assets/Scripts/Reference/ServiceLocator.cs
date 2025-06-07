using UnityEngine;
using System.Collections.Generic;
using System;

using TheRavine.Base;
public static class ServiceLocator
{
    private static readonly Dictionary<Type, MonoBehaviour> _services = new();
    private static readonly List<Transform> _playersTransforms = new();
    private static ILogger _logger;
    private static ISettingsModel _settingsModel;
    private static IWorldManager _worldManager;
    private static IWorldDataService _worldDataService;

    public static bool Register<T>(T service) where T : MonoBehaviour
    {
        Type type = typeof(T);
        if (!_services.ContainsKey(type))
        {
            _services[type] = service;
            return true;
        }
        return false;
    }

    public static void RegisterLogger(ILogger logger)
    {
        _logger = logger;
    }

    public static void RegisterSettings(ISettingsModel settingsModel)
    {
        _settingsModel = settingsModel;
    }

    public static void RegisterWorldManager(IWorldManager worldManager)
    {
        _worldManager = worldManager;
    }

    public static void RegisterWorldDataService(IWorldDataService worldDataService)
    {
        _worldDataService = worldDataService;
    }

    public static void RegisterPlayer<T>(T service) where T : MonoBehaviour
    {
        _playersTransforms.Add(service.transform);
    }

    public static T GetService<T>() where T : MonoBehaviour
    {
        Type type = typeof(T);
        if (_services.ContainsKey(type))
            return _services[type] as T;
        
        _logger?.LogError($"Сервис {typeof(T).Name} не зарегистрирован");
        return null;
    }

    public static T Get<T>() where T : class
    {
        if (typeof(T) == typeof(ILogger))
            return _logger as T;
            
        if (typeof(T) == typeof(ISettingsModel))
            return _settingsModel as T;

        if (typeof(T) == typeof(IWorldManager))
            return _worldManager as T;

        if (typeof(T) == typeof(IWorldDataService))
            return _worldDataService as T;

        if (_services.TryGetValue(typeof(T), out var service))
            return service as T;

        _logger?.LogError($"Сервис {typeof(T).Name} не зарегистрирован");
        throw new InvalidOperationException($"Сервис {typeof(T).Name} не зарегистрирован");
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        service = null;
        
        if (typeof(T) == typeof(ILogger))
        {
            service = _logger as T;
            return service != null;
        }
        
        if (typeof(T) == typeof(ISettingsModel))
        {
            service = _settingsModel as T;
            return service != null;
        }

        if (typeof(T) == typeof(IWorldManager))
        {
            service = _worldManager as T;
            return service != null;
        }

        if (typeof(T) == typeof(IWorldDataService))
        {
            service = _worldDataService as T;
            return service != null;
        }

        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = obj as T;
            return service != null;
        }
        
        return false;
    }

    public static ILogger GetLogger() => _logger;
    public static ISettingsModel GetSettings() => _settingsModel;
    public static IWorldManager GetWorldManager() => _worldManager;
    public static IWorldDataService GetWorldDataService() => _worldDataService;

    public static Transform GetPlayerTransform()
    {
        if (_playersTransforms.Count == 0)
        {
            _logger?.LogError("There is no players in the game");
            return null;
        }
        return _playersTransforms[0];
    }

    public static List<Transform> GetPlayersTransforms() => _playersTransforms;

    public static void Clear()
    {
        foreach (var service in _services.Values)
        {
            if (service != null && service is IDisposable disposable)
                disposable.Dispose();
        }
        
        _services.Clear();
        _playersTransforms.Clear();
        
        if (_logger is IDisposable loggerDisposable)
            loggerDisposable.Dispose();
        _logger = null;
        
        if (_settingsModel is IDisposable settingsDisposable)
            settingsDisposable.Dispose();
        _settingsModel = null;

        if (_worldManager is IDisposable worldManagerDisposable)
            worldManagerDisposable.Dispose();
        _worldManager = null;

        if (_worldDataService is IDisposable worldDataServiceDisposable)
            worldDataServiceDisposable.Dispose();
        _worldDataService = null;
    }
}