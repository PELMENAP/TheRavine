using UnityEngine;
[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New ObjectInfo")]
public class ObjectInfo : ScriptableObject
{
    public string prefabName;
    public int prefabID => Animator.StringToHash(prefabName);
    public ushort amount;
    public ushort poolSize;
    public InstanceType iType;
    public BehaviourType bType;
    public NAlInfo nalinfo;
    public InventoryItemInfo iteminfo;
    public GameObject prefab;
    public ObjectInfo nextStep;
    public Vector2Int[] addspace;
    public SpreadPattern deadPattern;
    public SpreadPattern pickUpPattern;
}

public enum BehaviourType
{
    None,
    NAL,
    GROW
}
