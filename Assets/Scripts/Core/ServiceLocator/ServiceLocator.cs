using R3;
using Cysharp.Threading.Tasks;
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

    public static async UniTask<T> WaitUntilServiceReady<T>() where T : class
    {
        T service;
        while (!ServiceLocator.Services.TryGet(out service))
        {
            await UniTask.Yield();
        }
        return service;
    }

    public static void ClearAll()
    {
        Services.Clear();
        Players.Clear();
    }
}