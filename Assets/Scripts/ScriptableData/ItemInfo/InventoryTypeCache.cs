using System.Collections.Generic;
using ZLinq;
using UnityEngine;

public static class InventoryTypeCache
{
    private static Dictionary<string, System.Type> _typeCache;
    private static bool _isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (_isInitialized) return;

        _typeCache = new Dictionary<string, System.Type>();
        
        var itemType = typeof(IInventoryItem);
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes().AsValueEnumerable()
                .Where(t => itemType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
            
            foreach (var type in types)
            {
                _typeCache[type.Name] = type;
            }
        }
        
        _isInitialized = true;
    }

    public static System.Type GetType(string typeName)
    {
        if (!_isInitialized) Initialize();
        
        return _typeCache.TryGetValue(typeName, out var type) ? type : null;
    }
}