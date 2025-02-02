using UnityEngine;
using System.Collections.Generic;

namespace TheRavine.Base
{
    public class GameStateMachine : MonoBehaviour
    {
        [SerializeField] private GameObject help, ui;
        [SerializeField] private Canvas inventoryCanvas;

        [SerializeField] private int standardStateMachineTickTime, tickPerUpdate;
        [SerializeField] private ServiceLocatorAccess serviceLocatorAccess;
        [SerializeField] private MonoBehaviour[] scriptsLoadedOnBootstrapState, scriptsLoadedOnInitialState, scriptsLoadedOnLoadingState;
        public StateMachine<GameStateMachine> StateMachine { get; private set; }
        private ServiceRegisterMachine serviceRegisterMachine;
        public void Initialize()
        {
            if(inventoryCanvas != null) inventoryCanvas.renderMode = RenderMode.WorldSpace;


            serviceRegisterMachine = new ServiceRegisterMachine(serviceLocatorAccess);
            StateMachine =  new StateMachine<GameStateMachine>(standardStateMachineTickTime,
                        new BootstrapState(this, serviceRegisterMachine.RegisterSomeServices(scriptsLoadedOnBootstrapState)),
                        new InitialState(this, serviceRegisterMachine.RegisterSomeServices(scriptsLoadedOnInitialState)),
                        new LoadingState(this, serviceRegisterMachine.RegisterSomeServices(scriptsLoadedOnLoadingState)),
                        new GameState(this));
        }
        public void StartGame()
        {
            if(help != null)
            {
                help.SetActive(false);
                ui.SetActive(false);
            }
            StateMachine.SwitchState<BootstrapState>();
        }
        public void OnGameAlreadyStarted()
        {
            if(help != null)
            {
                ui.SetActive(true);
                help.SetActive(true);
            }
            if(inventoryCanvas != null) inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        public void BreakUpServices() => serviceRegisterMachine.BreakUpServices();
        public int GetTickPerUpdate() => tickPerUpdate;
        public void StartNewServices(Queue<ISetAble> services, ISetAble.Callback callback) => serviceRegisterMachine.StartNewServices(services, callback);
    }
}