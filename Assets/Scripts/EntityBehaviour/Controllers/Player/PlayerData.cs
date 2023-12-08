using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerData : AEntity, ISetAble
{
    private ServiceLocator serviceLocator;
    public CM cameraMen;
    public Camera cachedCamera { get; private set; }
    public static PlayerData instance;
    public TextMeshProUGUI InputWindow;
    public float reloadSpeed, MOVEMENT_BASE_SPEED, maxMouseDis, CROSSHAIR_DISTANSE;
    public Action<Vector3> placeObject, aimRaise;
    public Joystick joystick;
    public Transform entityTrans, crosshair, playerMark;
    public Vector3 factMousePosition;
    private IControllable controller;

    public PlayerUI ui;
    // public bool moving = true;
    // private bool act = true;
    //Debug.Log("activate");

    #region [MONO]

    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        serviceLocator = locator;
        instance = this;
        entityTrans = this.transform;
        cameraMen.SetUp(null, locator);
        cachedCamera = cameraMen.mainCam;
        controller = GetComponent<IControllable>();
        // ui.AddSkill(new SkillRush(10f, 0.05f, 20), PData.pdata.dushParent, PData.pdata.dushImage, "Rush");
        controller.SetInitialValues();
        callback?.Invoke();
    }
    public void init() => Init();


    // private void Update()
    // {
    //     if (Input.GetKeyUp("="))
    //         ui.ActivateSkill("Rush");
    //     else if (Input.GetKeyUp("-"))
    //         ui.DeactivateSkill("Rush");
    // }

    private void FixedUpdate()
    {
        if (behaviourCurrent != null)
        {
            behaviourCurrent.Update();
            cameraMen.CameraUpdate();
        }
    }

    protected override void Init()
    {
        InitUI();
        InitBehaviour();
        SetBehaviourIdle();
    }

    private void InitUI()
    {
        ui = new PlayerUI();
    }

    protected override void InitBehaviour()
    {
        behavioursMap = new Dictionary<Type, IPlayerBehaviour>();
        Action idleAction = controller.Move;
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
    }

    public void SetBehaviourDialog()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourDialoge>());
    }

    public void SetBehaviourSit()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourSit>());
    }

    #endregion

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
        ui.UpdateSkills(reloadSpeed);
    }

    public void Priking()
    {
        StartCoroutine(Prick());
    }

    private IEnumerator Prick()
    {
        // moving = false;
        // animator.SetBool("isPrick", true);
        yield return new WaitForSeconds(1);
        // animator.SetBool("isPrick", false);
        // moving = true;
    }

    public void MoveTo(Vector2 newPosition)
    {
        entityTrans.position = newPosition;
    }

    public void SetMousePosition(Vector3 aim)
    {
        if (aim.x > maxMouseDis)
            aim.x = maxMouseDis;
        if (aim.y > maxMouseDis)
            aim.y = maxMouseDis;
        if (aim.x < -maxMouseDis)
            aim.x = -maxMouseDis;
        if (aim.y < -maxMouseDis)
            aim.y = -maxMouseDis;
        if (aim.magnitude < CROSSHAIR_DISTANSE + 1)
            aim = Vector2.zero;
        factMousePosition = aim;
    }
}