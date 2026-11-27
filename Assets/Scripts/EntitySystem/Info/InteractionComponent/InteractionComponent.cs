using TheRavine.EntityControl;
public interface IInteractionComponent : IComponent
{
}

public class InteractionComponent : IInteractionComponent
{
    private BindInfo bindInfo {get; set;}
    private AEntity entity;
    public InteractionComponent(BindInfo bindInfo, AEntity entity)
    {
        if(bindInfo == null) return;

        this.entity = entity;
    }

    public void Dispose()
    {

    }
}
