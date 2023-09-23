using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    public StateMachine<Bootstrap> StateMachine { get; private set; }

    [SerializeField] private InsObject[] instanceObjects;

    public void Start()
    {
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
        if(instanceObjects[i].gameObject == null)
            return;
        Instantiate(instanceObjects[i].gameObject, instanceObjects[i].position, Quaternion.identity);
    }

    public void StartGame()
    {
        Settings.SceneNumber = 0;
        StateMachine.SwitchState<BootstrapState>();
    }

[System.Serializable]
    public struct InsObject
    {
        public GameObject gameObject;
        public Vector3 position;
    }
}
