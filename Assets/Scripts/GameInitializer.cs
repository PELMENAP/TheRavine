using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

using TheRavine.Base;
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private Action<string> onMessageDisplayTerminal;
    [SerializeField] private Terminal terminal;

    private void Awake()
    {
        ServiceLocator.Clear();
        if (initializeOnAwake)
            InitializeServices();
    }

    public void InitializeServices()
    {
        onMessageDisplayTerminal += terminal.Display;

        ILogger logger = new Logger(onMessageDisplayTerminal);
        ServiceLocator.RegisterLogger(logger);

        terminal.Setup();
        
        var worldManager = new WorldManager(logger);
        var settingsModel = new SettingsModel();
        var worldDataService = new WorldDataService(worldManager, logger);

        ServiceLocator.RegisterSettings(settingsModel);
        ServiceLocator.RegisterWorldManager(worldManager);
        ServiceLocator.RegisterWorldDataService(worldDataService);
    }

    private void OnDisable()
    {
        onMessageDisplayTerminal -= terminal.Display;
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
