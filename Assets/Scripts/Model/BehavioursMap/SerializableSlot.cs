using MemoryPack;

[MemoryPackable]
public partial struct SerializableInventorySlot
{
    public string title;
    public int amount;

    public SerializableInventorySlot(string title, int amount)
    {
        this.title = title;
        this.amount = amount;
    }
}