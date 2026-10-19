using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SimpleUnitAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Should animation run only once?")]
    [SerializeField] private bool playOnce = false, isBird = false;

    [Tooltip("Frame duration in seconds.")]
    [SerializeField] private float frameDuration = 0.1f, speedLimit = 0.1f;

    [Tooltip("Animation frames.")]
    [SerializeField] private Sprite[] frames, defaults;

    private int currentFrame = 0, defaultPose;
    private bool isAnimation = true;
    private Vector3 previousPosition, currentPosition;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        previousPosition = this.transform.position;

        if (isBird && defaults != null && defaults.Length > 0) defaultPose = RavineRandom.RangeInt(0, defaults.Length);
        else isBird = false;
    }

    private void OnEnable()
    {
        if (frames == null || frames.Length == 0) return;
        isAnimation = true;
        previousPosition = this.transform.position;
        InvokeRepeating(nameof(NextFrame), RavineRandom.RangeFloat(0, frameDuration), frameDuration);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(NextFrame));
        currentFrame = 0;
    }

    private void NextFrame()
    {
        if (isBird && currentFrame == 0)
        {
            currentPosition = this.transform.position;
            if (Vector3.Distance(previousPosition, currentPosition) < speedLimit)
            {
                spriteRenderer.sprite = defaults[defaultPose];
                isAnimation = false;
            }
            else
            {
                isAnimation = true;
            }
            previousPosition = currentPosition;
        }

        if (!isAnimation) return;

        spriteRenderer.sprite = frames[currentFrame];
        currentFrame++;

        if (currentFrame < frames.Length) return;

        currentFrame = 0;
        if (isBird) defaultPose = RavineRandom.RangeInt(defaults.Length);
        if (playOnce)
        {
            isAnimation = false;
            CancelInvoke(nameof(NextFrame));
        }
    }
}
