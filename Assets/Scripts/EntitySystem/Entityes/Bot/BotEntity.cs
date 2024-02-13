
using System.Collections.Generic;
using UnityEngine;
using TMPro;

using TheRavine.EntityControl;
using TheRavine.Services;
public class BotEntity : AStatePatternData, ISetAble, IEntity
{
    [SerializeField] private EntityGameData _entityGameData;
    public EntityGameData entityGameData { get { return _entityGameData; } set { } }
    public BotEntity data;

    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        Init();
        SetBehaviourIdle();
        // crosshair.gameObject.SetActive(false);
        callback?.Invoke();
    }
    public void SetUpEntityData(EntityInfo _entityInfo)
    {
        _entityGameData = new EntityGameData(_entityInfo);
    }
    public Vector2 GetEntityPosition()
    {
        return new Vector2(this.transform.position.x, this.transform.position.y);
    }
    public void UpdateEntityCycle()
    {
        if (behaviourCurrent != null)
            behaviourCurrent.Update();
    }

    protected void InitBehaviour()
    {
        behavioursMap = new Dictionary<System.Type, IPlayerBehaviour>();
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

    public void BreakUp()
    {

    }

    public void EnableVeiw()
    {

    }
    public void DisableView()
    {

    }
}
