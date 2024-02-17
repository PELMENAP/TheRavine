using UnityEngine;
public interface IControllable
{
    void SetInitialValues();
    void SetZeroValues();
    void EnableComponents();
    void DisableComponents();
    void Move();
    void Jump();
    void Animate();
    void Aim();
}