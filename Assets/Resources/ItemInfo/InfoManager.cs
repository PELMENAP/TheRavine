using System;
using UnityEngine;

public class InfoManager
{
    public static IInventoryItem GetInventoryItem(string title, int amount = 1){
        Type itemType = Type.GetType(title, false, true);
        if (itemType != null)
        {
            InventoryItemInfo itemInfo = Resources.Load<InventoryItemInfo>("ItemInfo/" + title);
            IInventoryItem item = (IInventoryItem)Activator.CreateInstance(itemType, new object[] { itemInfo });
            item.state.amount = amount;
            return item;
        }
        else
            throw new Exception("There's no exist class");
    }
}
