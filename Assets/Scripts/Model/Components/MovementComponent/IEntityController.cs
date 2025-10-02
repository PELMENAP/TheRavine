using UnityEngine;
using TheRavine.EntityControl;
public interface IEntityController
{
    void SetInitialValues(AEntity entity, IRavineLogger logger);
    void SetZeroValues();
    void EnableComponents();
    void DisableComponents();
    void Delete();
    Transform GetModelTransform();
    Vector2 GetEntityVelocity();
}