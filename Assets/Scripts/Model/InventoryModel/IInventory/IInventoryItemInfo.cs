using UnityEngine;

public interface IInventoryItemInfo
{
    bool isPlaceable { get; }
    string id { get; }
    string title { get; }
    string description { get; }
    int maxItemsInInventorySlot { get; }
    Sprite spriteIcon { get; }
    Sprite infoSprite { get; }
    GameObject prefab { get; }
    SlotMask slotMask => SlotMask.Bag | SlotMask.Hotbar | SlotMask.Craft;
    bool Accepts(SlotKind kind) => (slotMask & kind.ToMask()) != 0;
}
