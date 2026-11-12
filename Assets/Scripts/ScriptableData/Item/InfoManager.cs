using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace TheRavine.Inventory
{
    public class InfoManager
    {
        private Dictionary<string, InventoryItemInfo> data;
        public InfoManager(DataItems _dataItems)
        {
            data = new Dictionary<string, InventoryItemInfo>();
            for (int i = 0; i < _dataItems.data.Count; i++) 
            {
                InventoryItemInfo info = _dataItems.data[i];
                if (info == null || string.IsNullOrEmpty(info.id))
                {
                    Debug.LogError($"[InfoManager] {_dataItems.name}: empty entry or id at index {i}");
                    continue;
                }
                if (!data.TryAdd(info.id, info))
                    Debug.LogError($"[InfoManager] duplicate item id {info.id} ({data[info.id].name}, {info.name})");
            }
        }
        public IInventoryItem GetInventoryItem(string id, int amount = 1)
        {   
            if (id == null || !data.TryGetValue(id, out var itemInfo))
            {
                Debug.LogError($"[InfoManager] there's no item with id {id} in DataItems");
                return null;
            }

            return Create(itemInfo, amount);
        }

        public IInventoryItem GetInventoryItemByInfo(InventoryItemInfo itemInfo, int amount = 1)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.id)) return null;
            return Create(itemInfo, amount);
        }

        private static IInventoryItem Create(InventoryItemInfo itemInfo, int amount)
        {
            Type itemType = InventoryTypeCache.Resolve(itemInfo.id);
            IInventoryItem item = itemType == typeof(DefaultItem)
                ? new DefaultItem(itemInfo)
                : (IInventoryItem)Activator.CreateInstance(itemType, new object[] { itemInfo });
            item.state.amount = amount;
            return item;
        }
        public InventoryCraftInfo[] GetAllCraftRecepts() => Resources.LoadAll("CraftInfo", typeof(InventoryCraftInfo)).Cast<InventoryCraftInfo>().ToArray();
    }
}