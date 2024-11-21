using System.Collections.Generic;

[System.Serializable]
public struct SerializableInventorySlot
{
    public string title;
    public int amount;

    public SerializableInventorySlot(string title, int amount)
    {
        this.title = title;
        this.amount = amount;
    }
}

[System.Serializable]
public class SerializableList<T> {
    public List<T> list = new();

}