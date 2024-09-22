using UnityEngine;
[CreateAssetMenu(fileName = "InventoryItemInfo", menuName = "Gameplay/Items/Create New ItemInfo")]

[System.Serializable]
public class InventoryItemInfo : ScriptableObject, IInventoryItemInfo
{
    [SerializeField] private bool _isPlaceable;
    [SerializeField] private string _id;
    [SerializeField] private string _title;
    [SerializeField] private int _maxItemsInInventorySlot;
    [SerializeField] private Sprite _spriteIcon;
    [SerializeField] private Sprite _infoSprite;
    [SerializeField] private GameObject _prefab;
    private string _description;
    public string description { get { return _description; } set { _description = value; } }
    public bool isPlaceable => _isPlaceable;
    public string id => _id;
    public string title => _title;
    public int maxItemsInInventorySlot => _maxItemsInInventorySlot;
    public Sprite spriteIcon => _spriteIcon;
    public Sprite infoSprite => _infoSprite;
    public GameObject prefab => _prefab;
}

