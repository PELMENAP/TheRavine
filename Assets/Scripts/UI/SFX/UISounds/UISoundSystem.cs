using UnityEngine;

public class UISoundSystem : MonoBehaviour
{
    public static UISoundSystem Instance { get; private set; }

    [SerializeField] private UISoundLibrary library;

    private AudioService audioService;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        audioService = AudioService.Instance;
        if (audioService == null)
            Debug.LogError("AudioService not found! UISoundSystem requires AudioService.");
    }

    public PooledAudio Play(UISoundType type, Vector3? position = null)
    {
        if (library == null || audioService == null) return null;

        var data = library.GetData(type);
        if (data == null || data.Clips.Length == 0) return null;

        var clip = data.Clips[UnityEngine.Random.Range(0, data.Clips.Length)];
        
        var config = AudioPlayConfig.Default(clip);
        config.Volume = data.Volume;
        config.Pitch = data.GetRandomPitch();
        config.Priority = data.Priority;
        config.Position = position ?? Vector3.zero;
        config.Is3D = false;

        return audioService.Play(AudioChannel.UI, config);
    }

    public PooledAudio PlayClick() => Play(UISoundType.Click);
    public PooledAudio PlayHover() => Play(UISoundType.Hover);
    public PooledAudio PlayDeny() => Play(UISoundType.Deny);
    public PooledAudio PlayConfirm() => Play(UISoundType.Confirm);
    public PooledAudio PlayOpen() => Play(UISoundType.Open);
    public PooledAudio PlayClose() => Play(UISoundType.Close);
    public PooledAudio PlaySuccess() => Play(UISoundType.Success);
    public PooledAudio PlayError() => Play(UISoundType.Error);
    public PooledAudio PlayNotification() => Play(UISoundType.Notification);
}