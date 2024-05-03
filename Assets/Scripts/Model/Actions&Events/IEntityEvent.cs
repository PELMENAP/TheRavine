
namespace TheRavine.Events
{

    public interface IEntityEvent
    {
    }
    public interface IEntityEvent<T>
    {
        T GetValue();
    }
}