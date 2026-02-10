using R3;
public static class ServiceLocator
{
    public static ServiceContainer Services { get; } = new ServiceContainer();
    public static T GetService<T>() where T : class
    {
        return Services.Get<T>();
    }
    public static PlayerContainer Players { get; } = new PlayerContainer();

    public static Observable<Unit> WhenPlayersNonEmpty()
    {
        if (Players.GetAllPlayers().Count > 0)
            return Observable.Return(Unit.Default);
        
        return Players.OnPlayersBecameNonEmpty;
    }

    public static void ClearAll()
    {
        Services.Clear();
        Players.Clear();
    }
}