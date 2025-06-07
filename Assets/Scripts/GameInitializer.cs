using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private bool initializeOnAwake = true;

    private void Awake()
    {
        if (initializeOnAwake)
            InitializeServices();
    }

    public void InitializeServices()
    {
        var worldManager = new WorldManager();
        var settingsModel = new SettingsModel();
        var worldDataService = new WorldDataService(worldManager);

        ServiceLocator.RegisterSettings(settingsModel);
    }

    private void OnDestroy()
    {
        ServiceLocator.Clear();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && ServiceLocator.TryGet<IWorldDataService>(out var worldDataService))
        {
            worldDataService.SaveWorldDataAsync().Forget();
        }
    }
}
