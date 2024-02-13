using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem.EnhancedTouch;

using TheRavine.Inventory;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.EntityControl;
using TheRavine.Services;

namespace TheRavine.Base
{
    using BootstrapStates;
    public class Bootstrap : MonoBehaviour
    {
        private ServiceLocator serviceLocator = new ServiceLocator();
        public byte tickPerUpdate;
        public StateMachine<Bootstrap> StateMachine { get; private set; }
        public Queue<ISetAble> _setAble = new Queue<ISetAble>();
        private Queue<ISetAble> _disAble = new Queue<ISetAble>();
        [SerializeField] private Camera _camera;
        [SerializeField] private DayCycle sun;
        [SerializeField] private MapGenerator generator;
        [SerializeField] private ObjectSystem objectSystem;
        [SerializeField] private UIInventory inventory;
        [SerializeField] private PlayerEntity playerData;
        [SerializeField] private Terminal terminal;
        [SerializeField] private EntitySystem entitySystem;
        [SerializeField] private MobGenerator mobGenerator;
        [SerializeField] private GameObject help, ui;
        private void Start()
        {
            EnhancedTouchSupport.Enable();
            serviceLocator.RegisterPlayer<PlayerEntity>();
            serviceLocator.Register<PlayerEntity>(playerData);
            serviceLocator.Register<ObjectSystem>(objectSystem);
            serviceLocator.Register<UIInventory>(inventory);
            serviceLocator.Register<MapGenerator>(generator);
            serviceLocator.Register<DayCycle>(sun);
            serviceLocator.Register<Terminal>(terminal);
            serviceLocator.Register<EntitySystem>(entitySystem);
            serviceLocator.Register<MobGenerator>(mobGenerator);
            _setAble.Enqueue((ISetAble)objectSystem);
            _setAble.Enqueue((ISetAble)playerData);
            _setAble.Enqueue((ISetAble)inventory);
            _setAble.Enqueue((ISetAble)generator);
            _setAble.Enqueue((ISetAble)sun);
            _setAble.Enqueue((ISetAble)terminal);
            _setAble.Enqueue((ISetAble)entitySystem);
            _setAble.Enqueue((ISetAble)mobGenerator);
            if (Settings.SceneNumber == 1 || Settings.SceneNumber == 0)
            {
                StateMachine = new StateMachine<Bootstrap>(
                new BootstrapState(this),
                new InitialState(this, Settings.isLoad),
                new LoadingState(this),
                new GameState(this));
            }
            else if (Settings.SceneNumber == 2)
            {
                StateMachine = new StateMachine<Bootstrap>(
            new BootstrapState(this),
            new InitialState(this, Settings.isLoad),
            new LoadingState(this),
            new GameState(this));
            }
            else if (Settings.SceneNumber == 3)
            {

            }
            StartGame();
            help.SetActive(false);
            ui.SetActive(false);
        }
        public void StartNewServise(ISetAble.Callback callback)
        {
            ISetAble setAble = _setAble.Dequeue();
            _disAble.Enqueue(setAble);
            setAble.SetUp(callback, serviceLocator);
        }
        public void StartGame()
        {
            Settings.SceneNumber = 0;
            StateMachine.SwitchState<BootstrapState>();
        }
        public void AddCameraToStack(Camera _cameraToAdd) => _camera.GetUniversalAdditionalCameraData().cameraStack.Add(_cameraToAdd);
        public void Finally()
        {
            ui.SetActive(true);
            help.SetActive(true);
            playerData.SetBehaviourIdle();
        }

        public void InTheEnd()
        {
            while (_disAble.Count > 0)
                _disAble.Dequeue().BreakUp();
            help.SetActive(false);
            ui.SetActive(false);
            EnhancedTouchSupport.Disable();
        }

        private void OnDisable()
        {
            InTheEnd();
        }
    }
}