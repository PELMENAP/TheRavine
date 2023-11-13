
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Bootstrap : MonoBehaviour
{
    public StateMachine<Bootstrap> StateMachine { get; private set; }

    [SerializeField] private ObjectInstance[] objectInstances;
    [SerializeField] private InsObject[] instanceObjects;
    [SerializeField] private Camera _camera;
    [SerializeField] private DayCycle sun;
    [SerializeField] private MapGenerator generator;
    public bool isTest;

    public void Start()
    {
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
        CreateBaseInstance();
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

    public void InstantiateRequiredPrefab(int i)
    {
        if ((i + 1) > instanceObjects.Length || instanceObjects[i].gameObject == null)
            return;
        //PoolManager.inst.ReuseObjectToPosition(instanceObjects[i].gameObject.GetInstanceID(), instanceObjects[i].position);
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

    public void StartGenerator()
    {
        generator.SetUp();
    }

    public void StartSun()
    {
        sun.StartUniverse();
    }

    [System.Serializable]
    public struct InsObject
    {
        public GameObject gameObject;
        public Vector2 position;
    }
}
