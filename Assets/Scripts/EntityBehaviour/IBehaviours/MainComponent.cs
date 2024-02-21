using TheRavine.EntityControl;
using UnityEngine;
public interface IMainComponent : IComponent
{
    EntityStats stats { get; set; }
    string name { get; set; }
    int prefabID { get; set; }
}

public class MainComponent : IMainComponent
{
    public EntityStats stats { get; set; }
    public string name { get; set; }
    public int prefabID { get; set; }
    public MainComponent(string _name, int _prefabID, EntityStats _stats)
    {
        stats = _stats;
        name = _name;
        prefabID = _prefabID;
    }

    public void Dispose()
    {

    }
}