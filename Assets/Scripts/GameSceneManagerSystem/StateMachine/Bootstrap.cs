
using UnityEngine;
using UnityEngine.Rendering.Universal; 

public class Bootstrap : MonoBehaviour
{
    public StateMachine<Bootstrap> StateMachine { get; private set; }

    [SerializeField] private InsObject[] instanceObjects;
    [SerializeField] private Camera _camera;
    [SerializeField] private DayCycle sun;
    public bool isTest; 

    public void Start()
    {
        // if(isTest){
        //     StateMachine = new StateMachine<Bootstrap>(
        //     new GameState(this));
        //     StateMachine.SwitchState<GameState>();
        //     return;
        // }
        if(Settings.SceneNumber == 1 || Settings.SceneNumber == 0){
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

    public void InstantiateRequiredPrefab(int i){
        if((i + 1) > instanceObjects.Length || instanceObjects[i].gameObject == null)
            return;
        PickUpRequire component = instanceObjects[i].gameObject.GetComponent<PickUpRequire>();
        if(component != null){
            InterObjectManager.instance.SetObjectByPosition(new Vector2(instanceObjects[i].position.x, instanceObjects[i].position.y), component.id, component.amount, instanceObjects[i].gameObject);        }
        else{
        Instantiate(instanceObjects[i].gameObject, instanceObjects[i].position, Quaternion.identity);}
    }

    public void StartGame()
    {
        Settings.SceneNumber = 0;
        StateMachine.SwitchState<BootstrapState>();
    }

    public void AddCameraToStack(Camera _cameraToAdd){
        var cameraData = _camera.GetUniversalAdditionalCameraData();
        cameraData.cameraStack.Add(_cameraToAdd);
    }

    public void StartSun(){
        sun.StartUniverse();
    }

[System.Serializable]
    public struct InsObject
    {
        public GameObject gameObject;
        public Vector3 position;
    }
}
