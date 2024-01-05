using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Bootstrap : MonoBehaviour
{
    private ServiceLocator serviceLocator = new ServiceLocator();
    public byte tickPerUpdate;
    public StateMachine<Bootstrap> StateMachine { get; private set; }
    private Queue<ISetAble> _setAble = new Queue<ISetAble>();
    [SerializeField] private Camera _camera;
    [SerializeField] private DayCycle sun;
    [SerializeField] private MapGenerator generator;
    [SerializeField] private ObjectSystem objectSystem;
    [SerializeField] private UIInventory inventory;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Terminal terminal;
    private void Awake()
    {
        serviceLocator.Register<PlayerData>(playerData);
        serviceLocator.Register<ObjectSystem>(objectSystem);
        serviceLocator.Register<UIInventory>(inventory);
        serviceLocator.Register<MapGenerator>(generator);
        serviceLocator.Register<DayCycle>(sun);
        serviceLocator.Register<Terminal>(terminal);
        _setAble.Enqueue((ISetAble)objectSystem);
        _setAble.Enqueue((ISetAble)playerData);
        _setAble.Enqueue((ISetAble)inventory);
        _setAble.Enqueue((ISetAble)generator);
        _setAble.Enqueue((ISetAble)sun);
        _setAble.Enqueue((ISetAble)terminal);
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
    }
    public void StartNewServise(ISetAble.Callback callback) => _setAble.Dequeue().SetUp(callback, serviceLocator);
    public void StartGame()
    {
        Settings.SceneNumber = 0;
        StateMachine.SwitchState<BootstrapState>();
    }
    public void AddCameraToStack(Camera _cameraToAdd) => _camera.GetUniversalAdditionalCameraData().cameraStack.Add(_cameraToAdd);
    public void Finally() => playerData.SetBehaviourIdle();

}
