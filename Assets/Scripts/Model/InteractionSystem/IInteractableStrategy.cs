using UnityEngine;

using TheRavine.EntityControl;
public interface IInteractableStrategy
{
    void Interact(GameObject interactor);
    void InteractWithEntity(AEntity interactorEntity);
}