using UnityEngine;

using TheRavine.EntityControl;

enum BuildingType
{
    EvolutionTower,
    DNAcenter,

}
public class BuildingInteraction : MonoBehaviour, IInteractable 
{
    public void Interact(GameObject interactor)
    {

    }

    public void InteractWithEntity(AEntity interactorEntity)
    {
        // interactorEntity.GetEntityComponent<EventBusComponent>().EventBus.Invoke(, BuildingType.EvolutionTower);
    }
}