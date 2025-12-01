using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

using TheRavine.Extensions;

namespace TheRavine.Base
{
    public class GameStateMachine : MonoBehaviour
    {
        [SerializeField] private GameObject help, ui;
        [SerializeField] private Canvas inventoryCanvas;

        [SerializeField] private int standardStateMachineTickTime, tickPerUpdate;
        [SerializeField] private MonoBehaviour[] scriptsLoadedOnBootstrapState, scriptsLoadedOnInitialState, scriptsLoadedOnLoadingState;
        public StateMachine<GameStateMachine> StateMachine;
        private ServiceRegisterMachine serviceRegisterMachine;
        private IRavineLogger ravineLogger;
        public void Initialize(IRavineLogger ravineLogger)
        {
            this.ravineLogger = ravineLogger;
            serviceRegisterMachine = new(ravineLogger);
            NetworkManager.Singleton.StartHost();

            if (inventoryCanvas != null) inventoryCanvas.renderMode = RenderMode.WorldSpace;
            StateMachine = new StateMachine<GameStateMachine>(standardStateMachineTickTime,
                        new BootstrapState(this, serviceRegisterMachine.RegisterSomeServices(scriptsLoadedOnBootstrapState)),
                        new InitialState(this, serviceRegisterMachine.RegisterSomeServices(scriptsLoadedOnInitialState)),
                        new LoadingState(this, serviceRegisterMachine.RegisterSomeServices(scriptsLoadedOnLoadingState)),
                        new GameState(this));
        }
        public void LogBootstrapInfo(string Message)
        {
            ravineLogger.LogWarning(Message);
        }
        public void StartGame()
        {
            if (help != null)
            {
                help.SetActive(false);
                ui.SetActive(false);
            }
            StateMachine.SwitchState<BootstrapState>();
        }
        public void OnGameAlreadyStarted()
        {
            if (help != null)
            {
                ui.SetActive(true);
                help.SetActive(true);
            }
            if (inventoryCanvas != null) inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        public void BreakUpServices()
        {
            serviceRegisterMachine?.BreakUpServices();
        }
        public int GetTickPerUpdate() => tickPerUpdate;
        public void StartNewServices(Queue<Pair<ISetAble, string>> services, ISetAble.Callback callback)
        {
            serviceRegisterMachine.StartNewServices(services, callback);
        }
    }
}