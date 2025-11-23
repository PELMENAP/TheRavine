using Unity.Netcode;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;

namespace TheRavine.EntityControl
{
    public class BotEntity : AEntity
    {
        private StatePatternComponent statePatternComponent;

        public BotEntity(EntityInfo entityInfo)
        {
            // _entityGameData = new EntityGameData(_entityInfo);
            statePatternComponent = new StatePatternComponent();
            base.AddComponentToEntity(statePatternComponent);
            // Init(null);
            SetBehaviourIdle();
            // crosshair.gameObject.SetActive(false);
        }
        public override void UpdateEntityCycle()
        {
            if (statePatternComponent.behaviourCurrent != null)
                statePatternComponent.behaviourCurrent.Update();
        }

        public override void Init()
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
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<BotBehaviourIdle>()).Forget();
        }

        public void SetBehaviourDialog()
        {
            statePatternComponent.SetBehaviourAsync(statePatternComponent.GetBehaviour<BotBehaviourDialoge>()).Forget();
        }
    }
}