using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Bootstrap : MonoBehaviour
{
    public StateMachine<Bootstrap> StateMachine { get; private set; }
    [SerializeField] private InsObject[] instanceObjects;
    private Queue<ISetAble> _setAble = new Queue<ISetAble>();
    [SerializeField] private Camera _camera;
    [SerializeField] private DayCycle sun;
    [SerializeField] private MapGenerator generator;
    [SerializeField] private ObjectSystem objectSystem;

    [SerializeField] private PlayerData playerData;

    public bool isTest;

    private void Awake()
    {
        _setAble.Enqueue((ISetAble)sun);
        _setAble.Enqueue((ISetAble)objectSystem);
        _setAble.Enqueue((ISetAble)generator);
        _setAble.Enqueue((ISetAble)playerData);
        // if(isTest){
        //     StateMachine = new StateMachine<Bootstrap>(
        //     new GameState(this));
        //     StateMachine.SwitchState<GameState>();
        //     return;
        // }
        if (Settings.SceneNumber == 1 || Settings.SceneNumber == 0)
        {
            StateMachine = new StateMachine<Bootstrap>(
            new BootstrapState(this),
            new InitialState(this),
            new LoadingState(this),
            new GameState(this));
        }
        else if (Settings.SceneNumber == 2)
        {
            if (Settings.isLoad)
            {
                StateMachine = new StateMachine<Bootstrap>(
            new BootstrapState(this),
            new InitialState(this),
            new LoadingState(this),
            new GameState(this));
            }
            else
            {
                StateMachine = new StateMachine<Bootstrap>(
            new BootstrapState(this),
            new InitialState(this),
            new LoadingState(this),
            new GameState(this));
            }
        }
        else if (Settings.SceneNumber == 3)
        {

        }
        StartGame();
    }

    public void CreateBaseInstance()
    {
        for (int i = 0; i < instanceObjects.Length; i++)
        {
            if (instanceObjects[i].gameObject == null)
                continue;
            // ObjectSystem.inst.PoolManagerBase.CreatePool(instanceObjects[i].gameObject, 5);
        }
    }

    public void StartNewServise()
    {
        _setAble.Dequeue().SetUp();
    }

    public void StartGame()
    {
        Settings.SceneNumber = 0;
        StateMachine.SwitchState<BootstrapState>();
    }

    public void AddCameraToStack(Camera _cameraToAdd)
    {
        var cameraData = _camera.GetUniversalAdditionalCameraData();
        cameraData.cameraStack.Add(_cameraToAdd);
    }

    [System.Serializable]
    public struct InsObject
    {
        public GameObject gameObject;
        public Vector2 position;
    }
}
