using UnityEngine;
[CreateAssetMenu(fileName = "InventoryCraftInfo", menuName = "Gameplay/Items/Create New CraftInfo")]
public class InventoryCraftInfo : ScriptableObject
{
    [SerializeField] private bool _isAvailable;
    [SerializeField] private int _timeToComplete;
    [SerializeField] private string _id;
    [SerializeField] private InventoryItemInfo[] _ingr;
    [SerializeField, Min(0)] private int[] _amountIngr;
    [SerializeField] private InventoryItemInfo _res;
    [SerializeField, Min(0)] private int _amountRes;
    public bool isAvailable => _isAvailable;
    public int timeToComplete => _timeToComplete;
    public string id => _id;
    public InventoryItemInfo[] ingr => _ingr;
    public InventoryItemInfo res => _res;
    public int[] amountIngr => _amountIngr;
    public int amountRes => _amountRes;
}
