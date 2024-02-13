using TheRavine.EntityControl;
using UnityEngine;
public interface IMainData
{
    EntityStats stats { get; set; }
    string name { get; set; }
    int prefabID { get; set; }
}