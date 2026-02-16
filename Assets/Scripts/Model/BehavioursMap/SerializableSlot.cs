using MemoryPack;

[MemoryPackable]
public partial struct SerializableInventorySlot
{
    public string title;
    public int amount;
    public bool isEmpty;

    public SerializableInventorySlot(string title, int amount)
    {
        this.title = title;
        this.amount = amount;
        this.isEmpty = string.IsNullOrEmpty(title);
    }
    
    public static SerializableInventorySlot Empty => new SerializableInventorySlot("", 0) 
    { 
        isEmpty = true 
    };
}