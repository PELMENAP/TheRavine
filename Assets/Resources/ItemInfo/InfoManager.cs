using System;
using System.Linq;
using UnityEngine;

public class InfoManager
{
    public static IInventoryItem GetInventoryItem(string title, int amount = 1)
    {
        Type itemType = Type.GetType(title, false, true);
        if (itemType != null)
        {
            InventoryItemInfo itemInfo = Resources.Load<InventoryItemInfo>("ItemInfo/" + title);
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

    public static IInventoryItem GetInventoryItemByInfo(string title, InventoryItemInfo itemInfo, int amount = 1)
    {
        if (itemInfo == null)
            throw new Exception("There's no info");
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
    public static InventoryCraftInfo[] GetAllCraftRecepts() => Resources.LoadAll("ItemInfo/CraftInfo", typeof(InventoryCraftInfo)).Cast<InventoryCraftInfo>().ToArray();

}
