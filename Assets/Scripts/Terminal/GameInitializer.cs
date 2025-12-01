using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

using TheRavine.Base;
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private Terminal terminal;
    private Action<string> onMessageDisplayTerminal;

    private void Awake()
    {
        ServiceLocator.ClearAll();
        DontDestroyOnLoad(this);

        if (initializeOnAwake)
            InitializeServices();
    }

    public void InitializeServices()
    {
        onMessageDisplayTerminal += terminal.Display;

        IRavineLogger logger = new RavineLogger(onMessageDisplayTerminal);
        ServiceLocator.Services.Register(logger);

        terminal.Setup(logger);

        IAsyncPersistentStorage persistenceStorage = new EncryptedPlayerPrefsStorage();

        GameSettingsManager gameSettingsManager = new(persistenceStorage);
        WorldFileManager worldFileManager = new(persistenceStorage);
        WorldSettingsManager worldSettingsManager = new(persistenceStorage);
        WorldService worldService = new(worldFileManager, worldSettingsManager);

        ServiceLocator.Services.Register(worldService);

        WorldManager worldManager = new(worldService, logger);
        SettingsModel settingsModel = new(gameSettingsManager, worldManager, worldService, logger);
        WorldDataService worldDataService = new(worldManager, worldService, logger);

        ServiceLocator.Services.Register(settingsModel);
        ServiceLocator.Services.Register(worldManager);
        ServiceLocator.Services.Register(worldDataService);
    }

    private void OnDisable()
    {
        onMessageDisplayTerminal -= terminal.Display;
        ServiceLocator.ClearAll();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && ServiceLocator.Services.TryGet<WorldDataService>(out var worldDataService))
        {
            worldDataService.SaveWorldDataAsync().Forget();
        }
    }
}
