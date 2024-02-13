using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

using TheRavine.EntityControl;
using TheRavine.Services;

public class PlayerEntity : AStatePatternData, ISetAble, IEntity
{
    [SerializeField] private EntityGameData _entityGameData;
    public EntityGameData entityGameData { get { return _entityGameData; } set { } }

    public CM cameraMen;
    [SerializeField] private PlayerDialogOutput output;
    public static PlayerEntity data; // не надо сингелтона
    public TextMeshProUGUI InputWindow; // не далжен игрок этим заниматься
    public float reloadSpeed, MOVEMENT_BASE_SPEED, maxMouseDis, CROSSHAIR_DISTANSE;
    public UnityAction<Vector2> placeObject, aimRaise, setMouse;
    public Vector3 factMousePosition;
    private IControllable controller;

    // public bool moving = true;
    // private bool act = true;
    //Debug.Log("activate");

    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        data = this;
        cameraMen.SetUp(null, locator);
        output.SetUp(null, locator);
        controller = GetComponent<IControllable>();
        // ui.AddSkill(new SkillRush(10f, 0.05f, 20), PData.pdata.dushParent, PData.pdata.dushImage, "Rush");
        controller.SetInitialValues();
        setMouse += SetMousePosition;
        Init();
        callback?.Invoke();
    }

    public void SetUpEntityData(EntityInfo _entityInfo)
    {
        _entityGameData = new EntityGameData(_entityInfo);
    }
    public Vector2 GetEntityPosition() => new Vector2(this.transform.position.x, this.transform.position.y);

    public void UpdateEntityCycle()
    {
        if (behaviourCurrent != null)
        {
            behaviourCurrent.Update();
            cameraMen.CameraUpdate();
        }
    }

    protected override void Init()
    {
        InitBehaviour();
    }

    protected void InitBehaviour()
    {
        behavioursMap = new Dictionary<System.Type, IPlayerBehaviour>();
        System.Action idleAction = controller.Move;
        idleAction += controller.Animate;
        idleAction += controller.Aim;
        idleAction += AimSkills;
        idleAction += ReloadSkills;
        PlayerBehaviourIdle Idle = new PlayerBehaviourIdle(controller, idleAction);
        behavioursMap[typeof(PlayerBehaviourIdle)] = Idle;
        idleAction = controller.Animate;
        idleAction += controller.Aim;
        idleAction += ReloadSkills;
        PlayerBehaviourDialoge Dialoge = new PlayerBehaviourDialoge();
        behavioursMap[typeof(PlayerBehaviourDialoge)] = Dialoge;
        idleAction = controller.Animate;
        idleAction += controller.Aim;
        idleAction += ReloadSkills;
        PlayerBehaviourSit Sit = new PlayerBehaviourSit(controller, idleAction);
        behavioursMap[typeof(PlayerBehaviourSit)] = Sit;
    }

    public void SetBehaviourIdle()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourIdle>());
        controller.EnableComponents();
    }

    public void SetBehaviourDialog()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourDialoge>());
        controller.DisableComponents();
    }

    public void SetBehaviourSit()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourSit>());
        controller.DisableComponents();
    }

    private void AimSkills()
    {
        // if (Input.GetKey("space") && Input.GetMouseButton(1))
        // {
        //     Vector3 playerPos = entityTrans.position;
        //     ui.UseSkill("Rush", aim, ref playerPos);
        //     entityTrans.position = playerPos;
        // }
    }

    private void ReloadSkills()
    {
    }

    public void Priking()
    {
        // StartCoroutine(Prick());
    }

    // private IEnumerator Prick()
    // {
    //     // moving = false;
    //     // animator.SetBool("isPrick", true);
    //     yield return new WaitForSeconds(1);
    //     // animator.SetBool("isPrick", false);
    //     // moving = true;
    // }

    public void MoveTo(Vector2 newPosition)
    {
        this.transform.position = newPosition;
    }

    public void SetMousePosition(Vector2 aim)
    {
        if (aim.magnitude > maxMouseDis)
            aim = aim.normalized * maxMouseDis;
        if (aim.magnitude < CROSSHAIR_DISTANSE + 1)
            aim = Vector2.zero;
        factMousePosition = aim;
    }

    public void BreakUp()
    {
        behavioursMap.Clear();
        cameraMen.BreakUp();
        output.BreakUp();
    }

    public void EnableVeiw()
    {

    }
    public void DisableView()
    {

    }
}