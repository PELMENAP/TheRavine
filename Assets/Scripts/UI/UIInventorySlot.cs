using UnityEngine.EventSystems;
using UnityEngine;

public class UIInventorySlot : UISlot
{
    public UIInventoryItem _uiInventoryItem;
    public IInventorySlot slot { get; private set; }

    private UIInventory _uiInventory;

    private void Awake()
    {
        _uiInventory = GetComponentInParent<UIInventory>();
        _uiInventoryItem.onDrop += OnDrop;
    }
    public void SetSlot(IInventorySlot newSlot)
    {
        slot = newSlot;
    }
    public override void OnDrop(PointerEventData eventData)
    {
        var otherItemUI = eventData.pointerDrag.GetComponent<UIInventoryItem>();
        var otherSlotUI = otherItemUI.GetComponentInParent<UIInventorySlot>();
        var otherSlot = otherSlotUI.slot;
        var inventory = _uiInventory.inventory;

        inventory.TransitFromSlotToSlot(this, otherSlot, slot);
        Refresh();
        otherSlotUI.Refresh();
    }

    public void Refresh()
    {
        if (slot != null)
            _uiInventoryItem.Refresh(slot);
    }

    public UIInventory GetInventory()
    {
        return _uiInventory;
    }
}
