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
    [SerializeField] private UIInventory inventory;
    [SerializeField] private PlayerData playerData;

    public bool result = true;

    private void Awake()
    {
        _setAble.Enqueue((ISetAble)objectSystem);
        _setAble.Enqueue((ISetAble)playerData);
        _setAble.Enqueue((ISetAble)inventory);
        _setAble.Enqueue((ISetAble)generator);
        _setAble.Enqueue((ISetAble)sun);
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
        _setAble.Dequeue().SetUp(ref result);
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

    public void Finally()
    {
        PlayerData.instance.init();
    }

    [System.Serializable]
    public struct InsObject
    {
        public GameObject gameObject;
        public Vector2 position;
    }
}
