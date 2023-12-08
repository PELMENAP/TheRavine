using UnityEngine;
public interface IObjectInfo
{
    string name { get; }
    ushort amount { get; }
    ushort poolSize { get; }
    InstanceType iType { get; }
    BehaviourType bType { get; }
    NAlInfo nalinfo { get; }
    GameObject prefab { get; }
}
