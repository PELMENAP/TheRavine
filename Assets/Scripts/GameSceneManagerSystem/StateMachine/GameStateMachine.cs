using UnityEngine;
using System;
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
        [SerializeField] private Action<string> onMessageDisplayTerminal;
        [SerializeField] private Terminal terminal;
        public StateMachine<GameStateMachine> StateMachine { get; private set; }
        private ServiceRegisterMachine serviceRegisterMachine;
        public void Initialize()
        {
            if(inventoryCanvas != null) inventoryCanvas.renderMode = RenderMode.WorldSpace;

            onMessageDisplayTerminal += terminal.Display;
            serviceRegisterMachine = new ServiceRegisterMachine(serviceLocatorAccess);
            serviceRegisterMachine.RegisterLogger(onMessageDisplayTerminal);
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

        public void BreakUpServices()
        {
            onMessageDisplayTerminal -= terminal.Display;
            serviceRegisterMachine.BreakUpServices();
        }
        public int GetTickPerUpdate() => tickPerUpdate;
        public void StartNewServices(Queue<ISetAble> services, ISetAble.Callback callback) => serviceRegisterMachine.StartNewServices(services, callback);
    }
}