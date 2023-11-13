using UnityEngine;
public interface IObject
{
    GameObject gameObject { get; set; }
    int prefabID { get; set; }
}