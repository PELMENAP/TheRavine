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
                data[_dataItems.data[i].title] = _dataItems.data[i];
            }
        }
        public IInventoryItem GetInventoryItem(string title, int amount = 1)
        {
            Debug.Log(title);
            
            Type itemType = InventoryTypeCache.GetType(title);
            
            if (itemType == null)
            {
                Debug.LogError($"Type {title} not found in cache");
                return null;
            }
            
            if (!data.TryGetValue(title, out var itemInfo))
            {
                Debug.Log($"there's no {title}");
                return null;
            }
            
            IInventoryItem item = (IInventoryItem)System.Activator.CreateInstance(itemType, new object[] { itemInfo });
            item.state.amount = amount;
            return item;
        }

        public IInventoryItem GetInventoryItemByInfo(string title, InventoryItemInfo itemInfo, int amount = 1)
        {
            if (itemInfo == null) return null;
            Type itemType = Type.GetType(title, true, false);
            if (itemType != null)
            {
                IInventoryItem item = (IInventoryItem)Activator.CreateInstance(itemType, new object[] { itemInfo });
                item.state.amount = amount;
                return item;
            }
            return null;
        }

        public Type GetItemType(InventoryItemInfo itemInfo)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.id)) return null;
            return Type.GetType(itemInfo.id, false, true);
        }
        public InventoryCraftInfo[] GetAllCraftRecepts() => Resources.LoadAll("CraftInfo", typeof(InventoryCraftInfo)).Cast<InventoryCraftInfo>().ToArray();
    }
}