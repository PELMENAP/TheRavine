using UnityEngine;

public class SFXSystem : MonoBehaviour
{
    public static SFXSystem Instance { get; private set; }

    [SerializeField] private SFXLibrary library;
    
    private AudioService audioService;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        audioService = AudioService.Instance;
    }

    public PooledAudio PlayRandom(SFXType type, Vector3? position = null)
    {
        if (library == null || audioService == null) return null;

        var collection = library.GetCollection(type);
        if (collection == null || collection.Clips.Length == 0) return null;

        var clip = collection.Clips[UnityEngine.Random.Range(0, collection.Clips.Length)];
        
        var config = AudioPlayConfig.Default(clip);
        config.Volume = collection.GetRandomVolume();
        config.Pitch = collection.GetRandomPitch();
        config.Position = position ?? Vector3.zero;
        config.Is3D = collection.Is3D && position.HasValue;
        config.Priority = collection.Priority;

        return audioService.Play(AudioChannel.SFX, config);
    }

    public PooledAudio PlayRandomAtPosition(SFXType type, Vector3 position) =>
        PlayRandom(type, position);
    
    public PooledAudio PlayFootstep(Vector3 position) => PlayRandomAtPosition(SFXType.Footstep, position);
    public PooledAudio PlayJump(Vector3 position) => PlayRandomAtPosition(SFXType.Jump, position);
    public PooledAudio PlayLand(Vector3 position) => PlayRandomAtPosition(SFXType.Land, position);
    public PooledAudio PlayExplosion(Vector3 position) => PlayRandomAtPosition(SFXType.Explosion, position);
    public PooledAudio PlayGunshot(Vector3 position) => PlayRandomAtPosition(SFXType.Gunshot, position);
}
