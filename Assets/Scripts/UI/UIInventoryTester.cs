using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
public class UIInventoryTester
{
    private UIInventorySlot[] _uiSlots;

    public InventoryWithSlots inventory { get; }

    public UIInventoryTester(UIInventorySlot[] uislots)
    {
        _uiSlots = uislots;

        inventory = new InventoryWithSlots(uislots.Length);
        inventory.OnInventoryStateChangedEvent += OnInventoryStateChanged;
    }

    public void FillSlots(bool filling)
    {
        if (filling)
        {
            // var allSlots = inventory.GetAllSlots();
            // var availableSlots = new List<IInventorySlot>(allSlots);
            // var filledSlots = 10;
            // for (int i = 0; i < filledSlots; i++)
            // {
            //     int index = 0;
            //     do
            //     {
            //         var rSlot = availableSlots[Random.Range(0, availableSlots.Count)];
            //         var item = PData.pdata.GetItem(index, Random.Range(1, 50));
            //         inventory.TryToAddSlot(this, rSlot, item);
            //         availableSlots.Remove(rSlot);
            //         index++;
            //     }
            //     while (PData.pdata.GetItem(index, 0) != null);
            // }
        }
        SetupInventoryUI(inventory);
    }

    private void SetupInventoryUI(InventoryWithSlots inventory)
    {
        var allSlots = inventory.GetAllSlots();
        var allSlotsCount = allSlots.Length;
        for (int i = 0; i < allSlotsCount; i++)
        {
            var slot = allSlots[i];
            var uiSlot = _uiSlots[i];
            uiSlot.SetSlot(slot);
            uiSlot.Refresh();
        }
    }

    private void OnInventoryStateChanged(object sender)
    {
        foreach (var uiSlot in _uiSlots)
        {
            uiSlot.Refresh();
        }
    }
}
