using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Reflection;

using TheRavine.EntityControl;
using TheRavine.Services;

namespace TheRavine.Base
{
    using BootstrapStates;
    public class Bootstrap : MonoBehaviour
    {
        private ServiceLocator serviceLocator;
        public byte tickPerUpdate;
        public StateMachine<Bootstrap> StateMachine { get; private set; }
        private Queue<ISetAble> _setAble;
        private Queue<ISetAble> _disAble;
        [SerializeField] private MonoBehaviour[] scripts;
        [SerializeField] private UniversalAdditionalCameraData _cameraData;
        [SerializeField] private GameObject help, ui;
        [SerializeField] private int standartStateMachineTickTime;
        private SceneTransitor trasitor;
        private void Start()
        {
            trasitor = new SceneTransitor();
            _setAble = new Queue<ISetAble>();
            _disAble = new Queue<ISetAble>();
            serviceLocator = new ServiceLocator();
            EnhancedTouchSupport.Enable();

            for (byte i = 0; i < scripts.Length; i++)
            {
                System.Type serviceType = scripts[i].GetType();
                MethodInfo registerMethod = typeof(ServiceLocator).GetMethod("Register").MakeGenericMethod(new System.Type[] { serviceType });
                registerMethod.Invoke(serviceLocator, new object[] { scripts[i] });
                _setAble.Enqueue((ISetAble)scripts[i]);
            }

            serviceLocator.RegisterPlayer<PlayerEntity>();

            switch (Settings.SceneNumber)
            {
                case 1:
                    StateMachine = new StateMachine<Bootstrap>(standartStateMachineTickTime,
                        new BootstrapState(this),
                        new InitialState(this, Settings.isLoad),
                        new LoadingState(this),
                        new GameState(this));
                    break;
                default:
                    StateMachine = new StateMachine<Bootstrap>(standartStateMachineTickTime,
                        new BootstrapState(this),
                        new InitialState(this, Settings.isLoad),
                        new LoadingState(this),
                        new GameState(this));
                    break;
            }
            StartGame();
        }
        public void StartNewServise(ISetAble.Callback callback)
        {
            if (_setAble.Count == 0)
                return;
            ISetAble setAble = _setAble.Dequeue();
            _disAble.Enqueue(setAble);
            setAble.SetUp(callback, serviceLocator);
        }
        public void StartGame()
        {
            help.SetActive(false);
            ui.SetActive(false);
            StateMachine.SwitchState<BootstrapState>();
        }
        public void AddCameraToStack(Camera _cameraToAdd) => _cameraData.cameraStack.Add(_cameraToAdd);
        public void Finally()
        {
            while (_setAble.Count > 0)
                StartNewServise(null);
            ui.SetActive(true);
            help.SetActive(true);
            serviceLocator.GetService<PlayerEntity>().SetBehaviourIdle();
        }

        public void SwitchToMainMenu(){
            InTheEnd(() => TransitToOtherScene());
        }

        private void InTheEnd(System.Action inTheEndCallback)
        {
            if(DataStorage.sceneClose) return;
            DataStorage.sceneClose = true;
            while (_disAble.Count > 0) _disAble.Dequeue().BreakUp();
            EnhancedTouchSupport.Disable();
            serviceLocator.Dispose();
            _setAble.Clear();
            _disAble.Clear();
            try
            {
                help.SetActive(false);
                ui.SetActive(false);
            }
            catch
            {
                // throw;
            }
            inTheEndCallback?.Invoke();
        }

        private void TransitToOtherScene(){
            trasitor.LoadScene(0).Forget();
            Settings.isLoad = false;
            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
            DataStorage.sceneClose = false;
        }

        private void OnDisable()
        {
            InTheEnd(() => Aboba());
        }

        private void Aboba(){
            print("test play");
        }
    }
}