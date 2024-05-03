using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class InfoManager
{
    private Dictionary<string, InventoryItemInfo> data;
    public InfoManager(DataItems _dataItems)
    {
        data = new Dictionary<string, InventoryItemInfo>();
        for(int i = 0; i < _dataItems.data.Count; i++) data[_dataItems.data[i].title] = _dataItems.data[i];
    }
    public IInventoryItem GetInventoryItem(string title, int amount = 1)
    {
        Type itemType = Type.GetType(title, false, true);
        if (itemType != null)
        {
            InventoryItemInfo itemInfo;
            data.TryGetValue(title, out itemInfo);
            if (itemInfo != null)
            {
                IInventoryItem item = (IInventoryItem)Activator.CreateInstance(itemType, new object[] { itemInfo });
                item.state.amount = amount;
                return item;
            }
            else    
                Debug.Log("there's no " + title);
        }
        return null;
    }

    public IInventoryItem GetInventoryItemByInfo(string title, InventoryItemInfo itemInfo, int amount = 1)
    {
        if (itemInfo == null) return null;
        Type itemType = Type.GetType(title, false, true);
        if (itemType != null)
        {
            IInventoryItem item = (IInventoryItem)Activator.CreateInstance(itemType, new object[] { itemInfo });
            item.state.amount = amount;
            return item;
        }
        return null;
    }
    public InventoryCraftInfo[] GetAllCraftRecepts() => Resources.LoadAll("CraftInfo", typeof(InventoryCraftInfo)).Cast<InventoryCraftInfo>().ToArray();

}
