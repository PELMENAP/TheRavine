public interface IMainComponent : IComponent
{
    string GetEntityName();
}

public class MainComponent : IMainComponent
{
    private string Name { get; set; }
    private int PrefabID { get; set; }
    
    public MainComponent(string _name, int _prefabID)
    {
        Name = _name;
        PrefabID = _prefabID;
    }

    public string GetEntityName() => Name;
    public int GetPrefabID() => PrefabID;
    public void Dispose()
    {

    }
}