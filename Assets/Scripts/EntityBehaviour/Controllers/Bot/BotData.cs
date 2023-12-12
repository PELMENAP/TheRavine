using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BotData : AEntityData, ISetAble
{
    public BotData data;

    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        Init();
        SetBehaviourIdle();
        // crosshair.gameObject.SetActive(false);
        callback?.Invoke();
    }
    private void FixedUpdate()
    {
        if (behaviourCurrent != null)
            behaviourCurrent.Update();
    }

    protected override void InitBehaviour()
    {
        behavioursMap = new Dictionary<Type, IPlayerBehaviour>();
        BotBehaviourIdle Idle = new BotBehaviourIdle();
        // Idle.ERef = this;
        behavioursMap[typeof(BotBehaviourIdle)] = Idle;
        BotBehaviourDialoge Dialoge = new BotBehaviourDialoge();
        behavioursMap[typeof(BotBehaviourDialoge)] = Dialoge;
        BotBehaviourSit Sit = new BotBehaviourSit();
        behavioursMap[typeof(BotBehaviourSit)] = Sit;
    }

    public void SetBehaviourIdle()
    {
        SetBehaviour(GetBehaviour<BotBehaviourIdle>());
    }

    public void SetBehaviourDialog()
    {
        SetBehaviour(GetBehaviour<BotBehaviourDialoge>());
    }

    public void SetSpeed()
    {
        // animator.SetFloat("Speed", 0);
    }
    public RoamMoveController moveController;
    public Animator animator;
    public Transform botTransform;
    public Rigidbody2D botRigidbody;
}
