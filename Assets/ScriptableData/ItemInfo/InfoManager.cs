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
                throw new Exception("There's no info");
        }
        else
            throw new Exception("There's no exist class");
    }

    public IInventoryItem GetInventoryItemByInfo(string title, InventoryItemInfo itemInfo, int amount = 1)
    {
        if (itemInfo == null) throw new Exception("There's no info");
        Type itemType = Type.GetType(title, false, true);
        if (itemType != null)
        {
            IInventoryItem item = (IInventoryItem)Activator.CreateInstance(itemType, new object[] { itemInfo });
            item.state.amount = amount;
            return item;
        }
        else
            throw new Exception("There's no exist class");
    }
    public InventoryCraftInfo[] GetAllCraftRecepts() => Resources.LoadAll("ItemInfo/CraftInfo", typeof(InventoryCraftInfo)).Cast<InventoryCraftInfo>().ToArray();

}
