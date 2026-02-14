using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System;

using TheRavine.Base;
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private bool initializeOnAwake = true, clearAllPlayerPrefs, createTestWorld;
    [SerializeField] private Terminal terminal;
    [SerializeField] private InputActionAsset inputAsset;

    private ActionMapController actionMapController;
    private Action<string> onMessageDisplayTerminal;

    private void Awake()
    {
        ServiceLocator.ClearAll();
        DontDestroyOnLoad(this);
        
        if(clearAllPlayerPrefs)
            PlayerPrefs.DeleteAll();

        if (initializeOnAwake)
            InitializeServices();
    }

    public void InitializeServices()
    {
        onMessageDisplayTerminal += terminal.Display;

        IRavineLogger logger = new RavineLogger(onMessageDisplayTerminal);
        ServiceLocator.Services.Register(logger);

        actionMapController = new ActionMapController(inputAsset, logger);
        ServiceLocator.Services.Register(actionMapController);

        terminal.gameObject.SetActive(true);
        terminal.Setup(logger, actionMapController);

        IAsyncPersistentStorage persistenceStorage = new EncryptedPlayerPrefsStorage();

        GlobalSettingsRepository globalSettingsRepository = new(persistenceStorage);

        WorldStateRepository worldStateRepository = new(persistenceStorage);
        WorldConfigRepository worldConfigRepository = new(persistenceStorage);


        WorldRegistry worldRegistry = new(worldStateRepository, worldConfigRepository, logger);
        SettingsMediator settingsMediator = new(globalSettingsRepository, worldConfigRepository, worldRegistry, logger);

        AutosaveSystem autosaveSystem = new(logger, 10);
        WorldStatePersistence worldStatePersistence = new(worldStateRepository, worldRegistry, autosaveSystem , logger);

        ServiceLocator.Services.Register(settingsMediator);
        ServiceLocator.Services.Register(worldRegistry);
        ServiceLocator.Services.Register(autosaveSystem);
        ServiceLocator.Services.Register(worldStatePersistence);

        if(createTestWorld)
        {
            worldRegistry.CreateWorldAsync("test").Forget();
        }
    }
    private void OnDestroy()
    {
        onMessageDisplayTerminal -= terminal.Display;
        ServiceLocator.ClearAll();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && ServiceLocator.Services.TryGet<WorldStatePersistence>(out var worldStatePersistence))
        {
            worldStatePersistence.SaveAsync().Forget();
        }
    }
}
