using TheRavine.Events;
public interface IEventBusComponent : IComponent
{
    EventBus EventBus { get; set; }
}
public class EventBusComponent : IEventBusComponent
{
    public EventBus EventBus { get; set; } = new EventBus();
    public void Dispose()
    {
        EventBus.Clear();
    }
}
