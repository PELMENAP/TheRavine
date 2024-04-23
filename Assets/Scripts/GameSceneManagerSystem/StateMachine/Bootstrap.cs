using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Collections.Generic;

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
        [SerializeField] private TrollMovementTransition trollMovementTransition;
        private SceneTransitor trasitor;
        private void Awake()
        {
            trollMovementTransition.finishAction += SwitchToParallaxScene;
            trasitor = new SceneTransitor();
            _setAble = new Queue<ISetAble>();
            _disAble = new Queue<ISetAble>();
            serviceLocator = new ServiceLocator();

            for (byte i = 0; i < scripts.Length; i++)
            {
                if(scripts[i] == null) continue;
                System.Type serviceType = scripts[i].GetType();
                MethodInfo registerMethod = typeof(ServiceLocator).GetMethod("Register").MakeGenericMethod(new System.Type[] { serviceType });
                registerMethod.Invoke(serviceLocator, new object[] { scripts[i] });
                _setAble.Enqueue((ISetAble)scripts[i]);
            }

            serviceLocator.RegisterPlayer<PlayerEntity>();

            StateMachine = Settings.SceneNumber switch
            {
                2 => new StateMachine<Bootstrap>(standartStateMachineTickTime,
                        new BootstrapState(this),
                        new InitialState(this, Settings.isLoad),
                        new LoadingState(this),
                        new GameState(this)),
                _ => new StateMachine<Bootstrap>(standartStateMachineTickTime,
                        new BootstrapState(this),
                        new InitialState(this, Settings.isLoad),
                        new LoadingState(this),
                        new GameState(this)),
            };
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
            TransitToOtherScene(0);
            // InTheEnd(() => TransitToOtherScene(0));
        }
        public void SwitchToParallaxScene(){
            TransitToOtherScene(1);
            // InTheEnd(() => TransitToOtherScene(1));
        }

        private void InTheEnd(System.Action inTheEndCallback)
        {
            if(DataStorage.sceneClose) return;
            DataStorage.sceneClose = true;
            while (_disAble.Count > 0) _disAble.Dequeue().BreakUp();
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
            finally
            {
                inTheEndCallback?.Invoke();
            }
        }

        private void TransitToOtherScene(int sceneNumber){
            trasitor.LoadScene(sceneNumber).Forget();
            Settings.isLoad = false;
            AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
            DataStorage.sceneClose = false;
        }

        private void OnDisable()
        {
            trollMovementTransition.finishAction -= SwitchToParallaxScene;
            InTheEnd(() => DebugLoad());
        }

        private void DebugLoad(){
            
        }
    }
}