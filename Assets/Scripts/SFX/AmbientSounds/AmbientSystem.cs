using UnityEngine;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class AmbientSystem : MonoBehaviour
{
    public static AmbientSystem Instance { get; private set; }

    [SerializeField] private AmbientLibrary library;
    
    private AudioService audioService;
    private AmbientType? currentAmbient;
    private List<AmbientLayerController> activeControllers = new();
    private CancellationTokenSource lifetimeCts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        lifetimeCts = new CancellationTokenSource();
    }

    private void Start()
    {
        audioService = AudioService.Instance;
    }

    private void OnDestroy()
    {
        StopAll();
        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
    }

    public async UniTask PlayAmbient(AmbientType type, float transitionTime = 2f)
    {
        if (library == null || audioService == null) return;

        var config = library.GetConfig(type);
        if (config == null) return;

        if (currentAmbient == type) return;

        if (currentAmbient.HasValue)
        {
            await StopAmbient(transitionTime);
        }

        currentAmbient = type;

        foreach (var layer in config.Layers)
        {
            var controller = new AmbientLayerController(layer, audioService, lifetimeCts.Token);
            activeControllers.Add(controller);
            controller.Start().Forget();
        }
    }

    public async UniTask StopAmbient(float fadeTime = 2f)
    {
        var tasks = new List<UniTask>();
        
        foreach (var controller in activeControllers)
        {
            tasks.Add(controller.Stop(fadeTime));
        }

        await UniTask.WhenAll(tasks);
        activeControllers.Clear();
        currentAmbient = null;
    }

    public void StopAll()
    {
        foreach (var controller in activeControllers)
            controller.Stop(0f).Forget();
        
        activeControllers.Clear();
        currentAmbient = null;
    }

    public void SetVolume(float volume)
    {
        foreach (var controller in activeControllers)
            controller.SetVolume(volume);
    }
}