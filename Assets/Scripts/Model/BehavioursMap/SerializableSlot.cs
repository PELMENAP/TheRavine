using MemoryPack;

[MemoryPackable]
public partial class SerializableInventorySlot
{
    public string title;
    public int amount;
    public bool isEmpty;

    public SerializableInventorySlot()
    {
        isEmpty = true;
        title = "";
        amount = 0;
    }

    public void SetEmpty()
    {
        isEmpty = true;
        title = "";
        amount = 0;
    }

    public void Set(string id, int amt)
    {
        isEmpty = false;
        title = id;
        amount = amt;
    }
}