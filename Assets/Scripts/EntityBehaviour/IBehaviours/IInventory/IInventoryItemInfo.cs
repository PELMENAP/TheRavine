using UnityEngine;

public interface IInventoryItemInfo
{
    string id { get; }
    string title { get; }
    string description { get; set; }
    int maxItemsInInventorySlot { get; }
    Sprite spriteIcon { get; }
    Sprite infoSprite { get; }
    GameObject prefab { get; }
}
