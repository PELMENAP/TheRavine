namespace TheRavine.Inventory
{
    public class InventorySerializer
    {
        public static SerializableList<SerializableInventorySlot> Serialize(IInventory inventory)
        {
            var data = new SerializableList<SerializableInventorySlot>();
            
            if (inventory is InventoryModel optimizedInventory)
            {
                var slots = optimizedInventory.GetAllSlots();
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot?.isEmpty != false)
                    {
                        data.list.Add(new SerializableInventorySlot("empty", 0));
                    }
                    else
                    {
                        data.list.Add(new SerializableInventorySlot(slot.item.info.id, slot.item.state.amount));
                    }
                }
            }
            
            return data;
        }
        
        public static void Deserialize(IInventory inventory, SerializableList<SerializableInventorySlot> data, InfoManager infoManager)
        {
            for (int i = 0; i < data.list.Count && i < inventory.capacity; i++)
            {
                var slotData = data.list[i];
                if (slotData.title != "empty" && slotData.amount > 0)
                {
                    var item = infoManager.GetInventoryItem(slotData.title, slotData.amount);
                    if (item != null)
                    {
                        inventory.TryToAdd(null, item);
                    }
                }
            }
        }
    }
}