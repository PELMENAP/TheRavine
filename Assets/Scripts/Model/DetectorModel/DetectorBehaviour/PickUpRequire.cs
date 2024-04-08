using UnityEngine;

public class PickUpRequire : MonoBehaviour
{
    public int amount;
    public string id;
    public InventoryItemInfo itemInfo;

    public void DestroyItself(){
        Destroy(this.gameObject);
    }
}
