using UnityEngine;
[CreateAssetMenu(fileName = "InventoryCraftInfo", menuName = "Gameplay/Items/Create New CraftInfo")]
public class InventoryCraftInfo : ScriptableObject, IInventoryCraftInfo
{
    [SerializeField] private bool _isorder;
    [SerializeField] private string _id;
    [SerializeField] private InventoryItemInfo[] _ingr;
    [SerializeField] private InventoryItemInfo _res;
    [SerializeField] private int[] _amountIngr;
    [SerializeField] private int _amountRes;
    public bool isorder => _isorder;
    public string id => _id;
    public InventoryItemInfo[] ingr => _ingr;
    public InventoryItemInfo res => _res;
    public int[] amountIngr => _amountIngr;
    public int amountRes => _amountRes;
}
