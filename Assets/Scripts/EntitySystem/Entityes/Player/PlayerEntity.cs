using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

using TheRavine.Services;
using TheRavine.Events;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(IControllable))]
    public class PlayerEntity : AEntity, ISetAble
    {

        public CM cameraMen;
        [SerializeField] private PlayerDialogOutput output;
        public static PlayerEntity data; // не надо сингелтона
        public TextMeshProUGUI InputWindow; // не далжен игрок этим заниматься
        public float reloadSpeed, MOVEMENT_BASE_SPEED, maxMouseDis, CROSSHAIR_DISTANSE;
        public UnityAction<Vector2> placeObject, aimRaise, setMouse;
        public Vector3 factMousePosition;
        private IControllable controller;
        private StatePatternComponent statePatternComponent;

        // public bool moving = true;
        // private bool act = true;
        //Debug.Log("activate");

        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            data = this;
            controller = this.GetComponent<IControllable>();
            statePatternComponent = new StatePatternComponent();

            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            // ui.AddSkill(new SkillRush(10f, 0.05f, 20), PData.pdata.dushParent, PData.pdata.dushImage, "Rush");
            controller.SetInitialValues();
            setMouse += SetMousePosition;
            Init();
            cameraMen.SetUp(null, locator);
            callback?.Invoke();
        }

        public override void SetUpEntityData(EntityInfo _entityInfo)
        {
            // _entityGameData = new EntityGameData(_entityInfo);
            base.AddComponentToEntity(new MainComponent(_entityInfo.name, _entityInfo.prefab.GetInstanceID(), new EntityStats(_entityInfo.statsInfo)));
        }
        public override Vector2 GetEntityPosition() => new Vector2(this.transform.position.x, this.transform.position.y);
        public override Vector2 GetEntityVelocity()
        {
            return new Vector2();
        }
        public override void UpdateEntityCycle()
        {
            if (statePatternComponent.behaviourCurrent != null)
            {
                statePatternComponent.behaviourCurrent.Update();
                cameraMen.CameraUpdate();
            }
        }

        public override void Init()
        {
            InitBehaviour();
        }

        private void InitBehaviour()
        {
            System.Action idleAction = controller.Move;
            idleAction += controller.Animate;
            idleAction += controller.Aim;
            idleAction += AimSkills;
            idleAction += ReloadSkills;
            PlayerBehaviourIdle Idle = new PlayerBehaviourIdle(controller, idleAction);
            statePatternComponent.AddBehaviour(typeof(PlayerBehaviourIdle), Idle);
            idleAction = controller.Animate;
            idleAction += controller.Aim;
            idleAction += ReloadSkills;
            PlayerBehaviourDialoge Dialoge = new PlayerBehaviourDialoge();
            statePatternComponent.AddBehaviour(typeof(PlayerBehaviourDialoge), Dialoge);
            idleAction = controller.Animate;
            idleAction += controller.Aim;
            idleAction += ReloadSkills;
            PlayerBehaviourSit Sit = new PlayerBehaviourSit(controller, idleAction);
            statePatternComponent.AddBehaviour(typeof(PlayerBehaviourSit), Sit);
            base.AddComponentToEntity(statePatternComponent);
        }

        public void SetBehaviourIdle()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourIdle>());
            controller.EnableComponents();
        }

        public void SetBehaviourDialog()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourDialoge>());
            controller.DisableComponents();
        }

        public void SetBehaviourSit()
        {
            statePatternComponent.SetBehaviour(statePatternComponent.GetBehaviour<PlayerBehaviourSit>());
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
            BreakUpEntity();
            cameraMen.BreakUp();
        }

        public override void EnableView()
        {

        }
        public override void DisableView()
        {

        }
    }
}