
using System.Collections.Generic;
using UnityEngine;
using TMPro;

using TheRavine.EntityControl;
using TheRavine.Services;
public class BotEntity : AEntity
{
    [SerializeField] private GameObject view;
    [SerializeField] private bool isActive;
    private StatePatternComponent statePatternComponent;

    public override void SetUpEntityData(EntityInfo _entityInfo)
    {
        // _entityGameData = new EntityGameData(_entityInfo);
        statePatternComponent = new StatePatternComponent();
        AddComponentToEntity(statePatternComponent);
        Init();
        SetBehaviourIdle();
        // crosshair.gameObject.SetActive(false);
        EnableView();
    }
    public override Vector2 GetEntityPosition()
    {
        return new Vector2(this.transform.position.x, this.transform.position.y);
    }
    public override void UpdateEntityCycle()
    {
        if (statePatternComponent.behaviourCurrent != null)
            statePatternComponent.behaviourCurrent.Update();
    }
    public override void Init()
    {
        InitBehaviour();
    }

    private void InitBehaviour()
    {
        BotBehaviourIdle Idle = new BotBehaviourIdle();
        Idle.AddCommand(new PrintMessageCommand("eboba"));
        statePatternComponent.AddBehaviour(typeof(BotBehaviourIdle), Idle);
        BotBehaviourDialoge Dialoge = new BotBehaviourDialoge();
        statePatternComponent.AddBehaviour(typeof(BotBehaviourDialoge), Dialoge);
        BotBehaviourSit Sit = new BotBehaviourSit();
        statePatternComponent.AddBehaviour(typeof(BotBehaviourSit), Sit);
    }

    public void SetBehaviourIdle()
    {
        statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<BotBehaviourIdle>());
    }

    public void SetBehaviourDialog()
    {
        statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<BotBehaviourDialoge>());
    }

    public void SetSpeed()
    {
        // animator.SetFloat("Speed", 0);
    }
    public RoamMoveController moveController;
    public Animator animator;
    public Transform botTransform;
    public Rigidbody2D botRigidbody;
    public override void EnableView()
    {
        isActive = true;
        view.SetActive(isActive);
    }
    public override void DisableView()
    {
        isActive = false;
        view.SetActive(isActive);
    }
}
