using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System;
using TheRavine.Base;

public class GameInitializer : MonoBehaviour
{
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private bool clearAllPlayerPrefs;
    [SerializeField] private bool createTestWorld;
    [SerializeField] private Terminal terminal;
    [SerializeField] private InputActionAsset inputAsset;

    private ActionMapController _actionMapController;
    private Action<string> _onMessageDisplayTerminal;

    private void Awake()
    {
        ServiceLocator.ClearAll();
        DontDestroyOnLoad(this);
        
        if (clearAllPlayerPrefs)
            PlayerPrefs.DeleteAll();

        if (initializeOnAwake)
            InitializeServices();
    }

    public void InitializeServices()
    {
        _onMessageDisplayTerminal += terminal.Display;

        RavineLogger logger = new RavineLogger(_onMessageDisplayTerminal);
        ServiceLocator.Services.Register(logger);

        _actionMapController = new ActionMapController(inputAsset, logger);
        ServiceLocator.Services.Register(_actionMapController);

        terminal.gameObject.SetActive(true);
        terminal.Setup(logger, _actionMapController);

        IAsyncPersistentStorage persistenceStorage = new EncryptedPlayerPrefsStorage();

        GlobalSettingsController globalSettings = new(persistenceStorage, logger);
        ServiceLocator.Services.Register(globalSettings);

        WorldRegistry worldRegistry = new(persistenceStorage, logger);
        ServiceLocator.Services.Register(worldRegistry);

        AutosaveCoordinator autosave = new(worldRegistry, logger);
        ServiceLocator.Services.Register(autosave);

        WorldSettingsController worldSettings = new(worldRegistry, autosave, logger);
        ServiceLocator.Services.Register(worldSettings);

        if (createTestWorld)
        {
            worldRegistry.CreateWorldAsync("test").Forget();
        }
    }

    private void OnDestroy()
    {
        _onMessageDisplayTerminal -= terminal.Display;
        
        if (ServiceLocator.Services.TryGet<AutosaveCoordinator>(out var autosave))
        {
            autosave.Stop();
        }

        ServiceLocator.ClearAll();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && ServiceLocator.Services.TryGet<WorldRegistry>(out var worldRegistry))
        {
            worldRegistry.SaveCurrentWorldAsync().Forget();
        }
    }
}