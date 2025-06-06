using UnityEngine;

using Random = TheRavine.Extensions.RavineRandom;

public class SimpleUnitAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    // [Tooltip("Should animation run only once?")]
    // [SerializeField] private bool playOnce = false;

    [Tooltip("Frame duration in seconds.")]
    [SerializeField] private float frameDuration = 0.1f, speedLimit = 0.1f;

    [Tooltip("Animation frames.")]
    [SerializeField] private Sprite[] frames, defaults;

    private int currentFrame = 0, defaultPose;
    private bool isAnimation;
    private Vector3 previousPosition, currentPosition;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        previousPosition = this.transform.position;
        defaultPose = Random.RangeInt(0, defaults.Length);
    }

    private void OnEnable()
    {
        if (frames == null || frames.Length == 0) return;
        InvokeRepeating(nameof(NextFrame), Random.RangeFloat(0, frameDuration), frameDuration);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(NextFrame));
        currentFrame = 0;
    }

    private void NextFrame()
    {   
        if (currentFrame == 0)
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

        if (isAnimation)
        {
            spriteRenderer.sprite = frames[currentFrame];
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                // if (playOnce)
                // {
                //     isAnimation = false;
                //     currentFrame = 0;
                //     CancelInvoke(nameof(NextFrame));
                // }
                defaultPose = Random.RangeInt(defaults.Length);
                currentFrame = 0;
            }
        }

        spriteRenderer.sprite = frames[currentFrame];
    }
}