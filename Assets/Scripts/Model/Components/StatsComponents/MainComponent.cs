using TheRavine.EntityControl;
public interface IMainComponent : IComponent
{
    string GetEntityName();
    EntityStats GetEntityStats();
}

public class MainComponent : IMainComponent
{
    private EntityStats stats { get; set; }
    private string name { get; set; }
    private int prefabID { get; set; }
    public MainComponent(string _name, int _prefabID, EntityStats _stats)
    {
        stats = _stats;
        name = _name;
        prefabID = _prefabID;
    }

    public string GetEntityName() => name;
    public EntityStats GetEntityStats() => stats;
    public void Dispose()
    {

    }
}