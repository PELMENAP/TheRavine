using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/Items/Create New ItemTagProvider")]
public class ItemTagProvider : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public InventoryItemInfo info;
        public string fullName;
        public string[] tags;
        public string material;
    }

    [SerializeField] private Entry[] _entries;
    private Dictionary<string, Entry> _lookup;

    public void Initialize()
    {
        _lookup = new Dictionary<string, Entry>(_entries.Length);
        foreach (var e in _entries)
            if (e.info != null) _lookup[e.info.id] = e;
    }

    public ItemContext GetContext(IInventoryItem item)
    {
        if (_lookup != null && _lookup.TryGetValue(item.info.id, out var entry))
            return new ItemContext { ItemName = entry.fullName, ItemTags = entry.tags, Material = entry.material };
        
        return new ItemContext { ItemName = item.info.id, ItemTags = Array.Empty<string>(), Material = string.Empty };
    }
}