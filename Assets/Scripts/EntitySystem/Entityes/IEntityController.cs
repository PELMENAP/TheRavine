using UnityEngine;
using TheRavine.EntityControl;
public interface IEntityController
{
    void SetInitialValues(AEntity entity, ILogger logger);
    void SetZeroValues();
    void EnableComponents();
    void DisableComponents();
    void Delete();
    Transform GetModelTransform();
}

public interface IMobControllable : IEntityController
{
    Vector2 GetEntityVelocity();
}