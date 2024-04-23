using UnityEngine;
public interface IController
{
    Vector2 GetMove();
    Vector2 GetAim();
    void MeetEnds();
    void EnableView();
    void DisableView();
}
