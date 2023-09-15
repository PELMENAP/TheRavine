using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotController : AEntity
{
    public BotData data;

    #region [MONO]

    private void Start()
    {
        Init();
        SetBehaviourIdle();
        // crosshair.gameObject.SetActive(false);
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
        Idle.ERef = this;
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
    #endregion


    // private IEnumerator Placing()
    // {
    //     aim = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
    //     aim.Normalize();
    //     crosshair.gameObject.SetActive(true);
    //     crosshair.localPosition = aim * CROSSHAIR_DISTANSE;
    //     crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
    //     yield return new WaitForSeconds(1);
    //     Instantiate(plob, crosshair.position, Quaternion.identity);
    //     crosshair.gameObject.SetActive(false);
    // }
}
