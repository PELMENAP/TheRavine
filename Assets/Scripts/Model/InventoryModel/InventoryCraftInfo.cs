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
    public bool IsAvailable => _isAvailable;
    public int TimeToComplete => _timeToComplete;
    public string Id => _id;
    public InventoryItemInfo[] Ingr => _ingr;
    public InventoryItemInfo Res => _res;
    public int[] AmountIngr => _amountIngr;
    public int AmountRes => _amountRes;
}
