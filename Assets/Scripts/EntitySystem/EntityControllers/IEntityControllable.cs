using UnityEngine;
using TheRavine.EntityControl;
public interface IEntityControllable
{
    void SetInitialValues(AEntity entity);
    void SetZeroValues();
    void EnableComponents();
    void DisableComponents();
    void Delete();
}

public interface IMobControllable : IEntityControllable
{
    Vector2 GetEntityVelocity();
    Transform GetModelTransform();
}