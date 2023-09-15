using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBehaviourSit : IPlayerBehaviour
{
    public Behaviour behaviourSit;

    public void Enter()
    {
        PlayerController.instance.reloadSpeed = 1.5f;
    }

    public void Exit()
    {

    }

    public void Update()
    {
        behaviourSit();
    }
}
