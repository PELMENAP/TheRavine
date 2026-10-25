using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SimpleUnitAnimator : MonoBehaviour
{
    public const int ClipFly = 0, ClipRest = 1, ClipLanding = 2, ClipTakeOff = 3;

    [Header("Animation Settings")]
    [Tooltip("Should animation run only once?")]
    [SerializeField] private bool playOnce = false;
    [Tooltip("Frames are driven externally via SetFrame.")]
    [SerializeField] private bool isBird = false;

    [Tooltip("Frame duration in seconds.")]
    [SerializeField] private float frameDuration = 0.1f;

    [Tooltip("Animation frames.")]
    [SerializeField] private Sprite[] frames, defaults, landingFrames, takeOffFrames;

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float elapsed;
    private bool isAnimation = true;

    public float FrameDuration => frameDuration;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (isBird) enabled = false;
    }

    private void OnEnable()
    {
        currentFrame = 0;
        isAnimation = frames != null && frames.Length > 0;
        elapsed = RavineRandom.RangeFloat(0f, frameDuration);
    }

    public int FrameCount(int clip) => Clip(clip).Length;

    public void SetFrame(int clip, int index)
    {
        Sprite[] c = Clip(clip);
        if (c.Length == 0) return;
        spriteRenderer.sprite = c[(uint)index < (uint)c.Length ? index : 0];
    }

    private Sprite[] Clip(int clip)
    {
        Sprite[] c = clip switch
        {
            ClipRest => defaults,
            ClipLanding => landingFrames,
            ClipTakeOff => takeOffFrames,
            _ => frames
        };
        if (c != null && c.Length > 0) return c;
        return frames ?? System.Array.Empty<Sprite>();
    }

    private void Update()
    {
        if (!isAnimation || frameDuration <= 0f) return;

        elapsed += Time.deltaTime;
        if (elapsed < frameDuration) return;
        elapsed -= frameDuration;

        spriteRenderer.sprite = frames[currentFrame];
        if (++currentFrame < frames.Length) return;

        currentFrame = 0;
        if (playOnce) isAnimation = false;
    }
}
