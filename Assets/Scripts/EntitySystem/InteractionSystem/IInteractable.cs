using UnityEngine;

using TheRavine.EntityControl;
public interface IInteractable
{
    void Interact(GameObject interactor);
    void InteractWithEntity(AEntity interactorEntity);
}