public static class ServiceLocator
{
    public static IServiceRegistry Services { get; } = new ServiceContainer();
    public static T GetService<T>() where T : class
    {
        return Services.Get<T>();
    }
    public static PlayerContainer Players { get; } = new PlayerContainer();

    public static void ClearAll()
    {
        Services.Clear();
        Players.Clear();
    }
}