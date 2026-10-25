using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System;
using TheRavine.Base;

public class GameInitializer : MonoBehaviour
{
    [SerializeField] private bool clearAllPlayerPrefs;
    [SerializeField] private bool createTestWorld;
    [SerializeField] private Terminal terminal;
    [SerializeField] private InputActionAsset inputAsset;

    private ActionMapController _actionMapController;
    private Action<string> _onMessageDisplayTerminal;

    public static GameInitializer Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(this);
        ServiceLocator.ClearAll();

        if (clearAllPlayerPrefs)
            PlayerPrefs.DeleteAll();
        InitializeServices();
    }

    public void InitializeServices()
    {
        _onMessageDisplayTerminal = terminal.Display;

        RavineLogger logger = new RavineLogger(_onMessageDisplayTerminal);
        ServiceLocator.Register(logger);
        RavineLog.Initialize(logger);

        _actionMapController = new ActionMapController(inputAsset, logger);
        ServiceLocator.Register(_actionMapController);

        terminal.gameObject.SetActive(true);
        terminal.Setup(logger, _actionMapController);

        IAsyncPersistentStorage persistenceStorage = new EncryptedPlayerPrefsStorage();

        GlobalSettingsController globalSettings = new(persistenceStorage, logger);
        ServiceLocator.Register(globalSettings);

        WorldRegistry worldRegistry = new(persistenceStorage, logger);
        ServiceLocator.Register(worldRegistry);

        AutosaveCoordinator autosave = new(worldRegistry, logger);
        ServiceLocator.Register(autosave);

        WorldSettingsController worldSettings = new(worldRegistry, autosave, logger);
        ServiceLocator.Register(worldSettings);

        if (createTestWorld)
        {
            worldRegistry.CreateWorldAsync("test").Forget();
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        _onMessageDisplayTerminal = null;
        ServiceLocator.ClearAll();
        RavineLog.Shutdown();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && ServiceLocator.Services.TryGet<WorldRegistry>(out var worldRegistry))
        {
            worldRegistry.SaveCurrentWorldAsync().Forget();
        }
    }
}