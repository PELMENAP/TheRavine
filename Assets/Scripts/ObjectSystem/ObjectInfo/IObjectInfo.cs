using UnityEngine;
public interface IObjectInfo
{
    ushort amount { get; }
    ushort poolSize { get; }
    InstanceType iType { get; }
    BehaviourType bType { get; }
    NAlInfo nalinfo { get; }
    GameObject prefab { get; }
    ObjectInfo nextStep { get; }
    Vector2[] addspace { get; }
    SpreadPattern deadPattern { get; }
    SpreadPattern pickUpPattern { get; }
}
