using TheRavine.EntityControl;
using UnityEngine;
public interface IEntity
{
    void SetUpEntityData(EntityInfo _entityInfo);
    EntityGameData entityGameData { get; set; }
    void UpdateEntityCycle();
    Vector2 GetEntityPosition();
    void EnableVeiw();
    void DisableView();

}