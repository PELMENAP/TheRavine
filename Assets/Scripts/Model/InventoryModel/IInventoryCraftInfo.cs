public interface IInventoryCraftInfo
{
    bool isorder { get; }
    string id { get; }
    InventoryItemInfo[] ingr { get; }
    InventoryItemInfo res { get; }
    int[] amountIngr { get; }
    int amountRes { get; }
}