using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerController : AEntity, IControllable
{
    public Camera cachedCamera;
    public static Transform entityTrans;
    public static PlayerController instance;
    public GameObject dialog;
    public TextMeshProUGUI InputWindow;
    public Rigidbody2D rb;
    public float reloadSpeed;
    public Action<Vector3> placeObject;

    #region [INSPECTOR]
    [SerializeField] private int score;
    [SerializeField] private float MOVEMENT_BASE_SPEED, CROSSHAIR_DISTANSE, movementSpeed, offset, timeLimit;
    [SerializeField] private Vector2 movementDirection, aim;
    [SerializeField] private Animator animator;
    public Transform crosshair;
    [SerializeField] private Transform playerMark;
    #endregion

    public PlayerUI ui;
    public bool moving = true;
    private bool act = true;
    //Debug.Log("activate");
    #region [MONO]
    private void Awake()
    {
        instance = this;
        entityTrans = this.transform;
        rb = (Rigidbody2D)this.GetComponent("Rigidbody2D");
        dialog.SetActive(false);
    }

    private void Start()
    {
        Init();
        ui.AddSkill(new SkillRush(10f, 0.05f, 20), PData.pdata.dushParent, PData.pdata.dushImage, "Rush");
    }

    private void Update()
    {
        if (Input.GetKeyUp("="))
            ui.ActivateSkill("Rush");
        else if (Input.GetKeyUp("-"))
            ui.DeactivateSkill("Rush");
    }

    private void FixedUpdate()
    {
        if (behaviourCurrent != null)
            behaviourCurrent.Update();
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
        PlayerBehaviourIdle Idle = new PlayerBehaviourIdle();
        Idle.behaviourIdle += MoveInternal;
        Idle.behaviourIdle += Animate;
        Idle.behaviourIdle += Aim;
        Idle.behaviourIdle += AimSkills;
        Idle.behaviourIdle += ReloadSkills;
        behavioursMap[typeof(PlayerBehaviourIdle)] = Idle;
        PlayerBehaviourDialoge Dialoge = new PlayerBehaviourDialoge();
        Dialoge.behaviourDialoge += Animate;
        Dialoge.behaviourDialoge += Aim;
        Dialoge.behaviourDialoge += ReloadSkills;
        behavioursMap[typeof(PlayerBehaviourDialoge)] = Dialoge;
        PlayerBehaviourSit Sit = new PlayerBehaviourSit();
        Sit.behaviourSit += Animate;
        Sit.behaviourSit += Aim;
        Sit.behaviourSit += ReloadSkills;
        behavioursMap[typeof(PlayerBehaviourSit)] = Sit;
    }

    public void SetBehaviourIdle()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourIdle>());
    }

    public void SetBehaviourDialog()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourDialoge>());
        movementSpeed = 0f;
        movementDirection = new Vector2(0, 0);
    }

    public void SetBehaviourSit()
    {
        SetBehaviour(GetBehaviour<PlayerBehaviourSit>());
        movementSpeed = 0f;
        movementDirection = new Vector2(0, 0);
    }

    #endregion

    public void Move(Vector2 direction){
        movementDirection = direction;
    }

    private void MoveInternal()
    {
        if (!moving)
            return;
        movementSpeed = Mathf.Clamp(movementDirection.magnitude, 0.0f, 1.0f);
        movementDirection.Normalize();
        rb.velocity = movementDirection * movementSpeed * MOVEMENT_BASE_SPEED;
        MoveMark();
    }


    public void Jump(){

    }

    private void MoveMark()
    {
        playerMark.position = new Vector3(entityTrans.position.x, entityTrans.position.y, 99);
        if (movementSpeed > 0.5f)
            playerMark.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90);
    }

    private void Animate()
    {
        if (movementDirection != Vector2.zero)
        {
            animator.SetFloat("Horizontal", movementDirection.x);
            animator.SetFloat("Vertical", movementDirection.y);
        }
        animator.SetFloat("Speed", movementSpeed);
    }

    private void Aim()
    {
        if (Input.GetMouseButton(1))
        {
            aim = cachedCamera.ScreenToWorldPoint(Input.mousePosition) - entityTrans.position;
            if (aim.magnitude > 2)
            {
                aim.Normalize();
                crosshair.localPosition = aim * CROSSHAIR_DISTANSE;
            }
            else
                crosshair.localPosition = aim;
            crosshair.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + offset);
            crosshair.gameObject.SetActive(true);
            AimPlace();
        }
        else
            crosshair.gameObject.SetActive(false);
    }

    private void AimPlace()
    {
        if (Input.GetMouseButton(0))
        {
            if (act)
                StartCoroutine(In());
        }
    }
    private IEnumerator In()
    {
        act = false;
        placeObject?.Invoke(crosshair.position);
        yield return new WaitForSeconds(timeLimit);
        act = true;
    }

    private void AimSkills()
    {
        if (Input.GetKey("space") && Input.GetMouseButton(1))
        {
            Vector3 playerPos = entityTrans.position;
            ui.UseSkill("Rush", aim, ref playerPos);
            entityTrans.position = playerPos;
        }
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
        moving = false;
        animator.SetBool("isPrick", true);
        yield return new WaitForSeconds(1);
        animator.SetBool("isPrick", false);
        moving = true;
    }
}