using UnityEngine;
using TheRavine.EntityControl;
public interface IEntityControllable
{
    void SetInitialValues(AEntity entity);
    void SetZeroValues();
    void EnableComponents();
    void DisableComponents();
}

public interface IMobControllable : IEntityControllable
{
    void UpdateMobControllerCycle();
}