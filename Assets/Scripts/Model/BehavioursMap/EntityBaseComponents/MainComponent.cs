public interface IMainComponent : IComponent
{
    string GetEntityName();
    ulong GetClientID();
}

public class MainComponent : IMainComponent
{
    private string Name { get; }
    private int PrefabID { get;}
    private ulong ClientID {get; set;}
    
    public MainComponent(string _name, int _prefabID, ulong _clientID)
    {
        Name = _name;
        PrefabID = _prefabID;
        ClientID = _clientID;
    }

    public string GetEntityName() => Name;
    public int GetPrefabID() => PrefabID;
    public ulong GetClientID() => ClientID;
    public void Dispose()
    {

    }
}