using UnityEngine;
using NaughtyAttributes;

using TheRavine.Security;
public class InventorySaveTester : MonoBehaviour
{
    SerializableList<SerializableInventorySlot> data = new();
    [Button] 
    public void SaveData()
    {
        SerializableList<SerializableInventorySlot> data1 = new();
        data1.list.Add(new SerializableInventorySlot("abiba", 10));
        SaveLoad.SaveEncryptedData(nameof(SerializableList<SerializableInventorySlot>), data1);
    }

    // [Button]
    // public void LoadData()
    // {
    //     data = SaveLoad.LoadEncryptedData<SerializableList<SerializableInventorySlot>>(nameof(SerializableList<SerializableInventorySlot>));
    // }

    // [Button]
    // public void ShowData()
    // {
    //     for(int i = 0; i < data.list.Count; i++){
    //         print(data.list[i].title + " " + data.list[i].amount);
    //     }
    // }
}
