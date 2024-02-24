using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

using TheRavine.Services;
using TheRavine.Events;

namespace TheRavine.EntityControl
{
    [RequireComponent(typeof(IEntityControllable))]
    public class PlayerEntity : AEntity, ISetAble
    {
        [SerializeField] private EntityInfo playerInfo;
        public CM cameraMen;
        [SerializeField] private PlayerDialogOutput output;
        public static PlayerEntity data; // не надо сингелтона
        public TextMeshProUGUI InputWindow; // не далжен игрок этим заниматься
        public UnityAction<Vector2> placeObject, aimRaise;
        public Vector3 factMousePosition;
        private IEntityControllable controller;
        private StatePatternComponent statePatternComponent;

        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            data = this;
            controller = this.GetComponent<IEntityControllable>();
            statePatternComponent = new StatePatternComponent();
            base.AddComponentToEntity(statePatternComponent);
            base.AddComponentToEntity(new EventBusComponent());
            base.AddComponentToEntity(new SkillComponent());
            Init();
            cameraMen.SetUp(null, locator);
            callback?.Invoke();
        }

        public override void SetUpEntityData(EntityInfo _entityInfo)
        {
            // _entityGameData = new EntityGameData(_entityInfo);
            base.AddComponentToEntity(new MainComponent(_entityInfo.name, _entityInfo.prefab.GetInstanceID(), new EntityStats(_entityInfo.statsInfo)));
            base.AddComponentToEntity(new MovementComponent(new EntityMovementBaseStats(_entityInfo.movementStatsInfo)));
            base.AddComponentToEntity(new AimComponent(new EntityAimBaseStats(_entityInfo.aimStatsInfo)));
            controller.SetInitialValues(this);
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
            SetUpEntityData(playerInfo);
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