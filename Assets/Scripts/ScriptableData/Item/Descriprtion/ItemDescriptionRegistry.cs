using System;
using System.Collections.Generic;

public sealed class ItemDescriptionRegistry
{
    private readonly Dictionary<Type, Func<IInventoryItem, string>> _providers = new();
    private readonly Dictionary<Type, string> _dynamicDescriptions = new();
    private readonly Dictionary<Type, float> _dynamicTimestamps = new();
    private Func<IInventoryItem, string> _fallback = item => item.info.description;

    public ItemDescriptionRegistry Register<T>(Func<IInventoryItem, string> provider)
        where T : IInventoryItem
    {
        _providers[typeof(T)] = provider;
        return this;
    }

    public ItemDescriptionRegistry WithFallback(Func<IInventoryItem, string> fallback)
    {
        _fallback = fallback;
        return this;
    }

    public string Get(IInventoryItem item)
    {
        if (item == null) return string.Empty;

        if (_dynamicDescriptions.TryGetValue(item.type, out var dynamic))
            return dynamic;

        return _providers.TryGetValue(item.type, out var provider)
            ? provider(item)
            : _fallback(item);
    }

    public void SetDynamic(IInventoryItem item, string description)
    {
        _dynamicDescriptions[item.type] = description;
        _dynamicTimestamps[item.type] = UnityEngine.Time.realtimeSinceStartup;
    }

    public bool IsStale(IInventoryItem item, float thresholdSeconds)
    {
        if (!_dynamicTimestamps.TryGetValue(item.type, out var ts))
            return true;

        return UnityEngine.Time.realtimeSinceStartup - ts > thresholdSeconds;
    }
}