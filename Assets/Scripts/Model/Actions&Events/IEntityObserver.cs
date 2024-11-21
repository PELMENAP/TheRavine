namespace TheRavine.Events
{
    public interface IEntityObserver
    {
        void OnEvent(IEntityEvent entityEvent);
    }
    public interface IEntityObserver<T>
    {
        void OnEvent(IEntityEvent<T> entityEvent);
    }
}
