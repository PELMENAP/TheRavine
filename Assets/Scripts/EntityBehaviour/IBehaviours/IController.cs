using UnityEngine;
public interface IController
{
    Vector2 GetMove();
    void GetJump();
    void MeetEnds();
    void EnableView();
    void DisableView();
}
