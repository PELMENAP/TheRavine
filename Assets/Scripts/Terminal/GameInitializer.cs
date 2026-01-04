using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

using TheRavine.Base;
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private bool initializeOnAwake = true, clearAllPlayerPrefs, createTestWorld;
    [SerializeField] private Terminal terminal;
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

        terminal.gameObject.SetActive(true);
        terminal.Setup(logger);

        IAsyncPersistentStorage persistenceStorage = new EncryptedPlayerPrefsStorage();

        GlobalSettingsRepository globalSettingsRepository = new(persistenceStorage);
        WorldStateRepository worldStateRepository = new(persistenceStorage);
        WorldConfigRepository WorldConfigRepository = new(persistenceStorage);
        WorldStorage worldStorage = new(worldStateRepository, WorldConfigRepository);

        ServiceLocator.Services.Register(worldStorage);

        WorldRegistry worldRegistry = new(worldStorage, logger);
        SettingsMediator settingsMediator = new(globalSettingsRepository, WorldConfigRepository, worldRegistry, logger);
        WorldStatePersistence worldStatePersistence = new(worldStateRepository, worldRegistry , logger);

        ServiceLocator.Services.Register(settingsMediator);
        ServiceLocator.Services.Register(worldRegistry);
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
