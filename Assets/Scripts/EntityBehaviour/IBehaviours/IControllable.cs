using UnityEngine;
public interface IControllable
{
    void SetInitialValues();
    void SetZeroValues();
    void Move();
    void Jump();
    void Animate();
    void Aim();
}