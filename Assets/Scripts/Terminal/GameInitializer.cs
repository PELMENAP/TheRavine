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
        ServiceLocator.RegisterService(logger);

        terminal.Setup(logger);

        var persistenceStorage = new EncryptedPlayerPrefsStorage();

        var gameSettingsManager = new GameSettingsManager(persistenceStorage);
        var worldFileManager = new WorldFileManager(persistenceStorage);
        var worldSettingsManager = new WorldSettingsManager(persistenceStorage);
        var worldService = new WorldService(worldFileManager, worldSettingsManager);

        ServiceLocator.RegisterService(worldService);

        var worldManager = new WorldManager(worldService, logger);
        var settingsModel = new SettingsModel(gameSettingsManager, worldManager, worldService, logger);
        var worldDataService = new WorldDataService(worldManager, worldService, logger);

        ServiceLocator.RegisterService(settingsModel);
        ServiceLocator.RegisterService(worldManager);
        ServiceLocator.RegisterService(worldDataService);
    }

    private void OnDisable()
    {
        onMessageDisplayTerminal -= terminal.Display;
        ServiceLocator.Clear();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && ServiceLocator.TryGetService<IWorldDataService>(out var worldDataService))
        {
            worldDataService.SaveWorldDataAsync().Forget();
        }
    }
}
