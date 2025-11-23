using UnityEngine.InputSystem;

using TheRavine.EntityControl;
public interface IInteractionComponent : IComponent
{
    void SetInteractableObject(IInteractable interactable);
}

public class InteractionComponent : IInteractionComponent
{
    private BindInfo bindInfo {get; set;}
    private IInteractable _currentInteractable;
    private AEntity entity;
    public InteractionComponent(BindInfo bindInfo, AEntity entity)
    {
        if(bindInfo == null) return;
        bindInfo.fastInteractAction.action.performed += OnFastInteractPerformed;
        bindInfo.delayInteractAction.action.performed += OnDelayInteractPerformed;

        this.entity = entity;
    }

    private void OnFastInteractPerformed(InputAction.CallbackContext context)
    {
        if (_currentInteractable != null)
        {
            _currentInteractable.InteractWithEntity(entity);
        }
    }

    private void OnDelayInteractPerformed(InputAction.CallbackContext context)
    {
        if (_currentInteractable != null)
        {
            _currentInteractable.InteractWithEntity(entity);
        }
    }
    
    public void SetInteractableObject(IInteractable interactable)
    {
        _currentInteractable = interactable;
    }
    public void Dispose()
    {

    }
}