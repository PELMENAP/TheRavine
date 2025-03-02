using UnityEngine;

using TheRavine.Extensions;

public class SimpleAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Should animation run only once?")]
    public bool playOnce = false;

    [Tooltip("Frame duration in seconds.")]
    public float frameDuration = 0.1f;

    [Tooltip("Animation frames.")]
    public Sprite[] frames, defaults;

    private int currentFrame = 0, defaultPose;
    private bool isAnimation;
    private Vector3 previousPosition;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        previousPosition = this.transform.position;
        defaultPose = RavineRandom.RangeInt(0, defaults.Length);
    }

    private void OnEnable()
    {
        isAnimation = true;
        if (frames == null || frames.Length == 0) return;
        InvokeRepeating(nameof(NextFrame), 0, frameDuration);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(NextFrame));
        currentFrame = 0;
    }

    private void NextFrame()
    {   
        if(currentFrame == 0)
        {
            if(Vector3.Distance(previousPosition, this.transform.position) < 0.1f)
            {
                isAnimation = false;
                spriteRenderer.sprite = defaults[defaultPose];
            } 
            else
            {
                isAnimation = true;
            }
            previousPosition = this.transform.position;
        }


        if (frames == null || frames.Length == 0 || !isAnimation) return;

        spriteRenderer.sprite = frames[currentFrame];
        currentFrame++;

        if (currentFrame >= frames.Length)
        {
            if (playOnce)
            {
                CancelInvoke(nameof(NextFrame));
            }
            defaultPose = RavineRandom.RangeInt(0, defaults.Length);
            currentFrame = 0;
        }
    }
}