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

    private string[] facts =  { "Хочет знать как устроен мир", "Слышал, что кириешки можно использовать для розжига", };

    public PlayerContext GetPlayerContext() => new PlayerContext
        {
            Name = Name,
            ProfessionId = "слесарь 4 разряда",
            Expertise = 0.7f,
            Doubt = 0.3f,
            KnownFacts = facts,
        };


    public void Dispose()
    {

    }
}