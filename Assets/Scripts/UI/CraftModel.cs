namespace TheRavine.Inventory
{
    public class CraftModel
    {
        public InventoryItemInfo ResultItemInfo { get; set; }
        public int ResultCount { get; set; }
        public int CraftDelay { get; set; }
        public bool IsPossible { get; set; }
    }
}