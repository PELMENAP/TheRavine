using UnityEngine;
public interface IController
{
    Vector2 GetMove();
    Vector2 GetAim();
    float GetJump();
    void MeetEnds();
    void EnableView();
    void DisableView();
}
