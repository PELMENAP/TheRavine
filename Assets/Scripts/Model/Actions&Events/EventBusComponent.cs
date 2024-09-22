using TheRavine.Events;
public interface IEventBusComponent : IComponent
{
    EventBusByName EventBus { get; set; }
}
public class EventBusComponent : IEventBusComponent
{
    public EventBusByName EventBus { get; set; } = new EventBusByName();
    public void Dispose()
    {
        EventBus.Dispose();
    }
}
