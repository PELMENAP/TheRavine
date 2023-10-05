using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void Behaviour();
public interface IPlayerBehaviour
{
    void Enter();
    void Exit();
    void Update();
}