using System;
using UnityEngine;

public class Fader : MonoBehaviour
{
    private const string Fader_path = "Fader";

    [SerializeField] private Animator animator;
    private static Fader _instance;
    public static Fader instance
    {
        get
        {
            if (_instance == null)
            {
                var prefab = Resources.Load<Fader>(Fader_path);
                _instance = Instantiate(prefab);
                DontDestroyOnLoad(_instance.gameObject);
            }
            return _instance;
        }
    }

    public bool isFading { get; private set; }

    private Action _fadedInCallBack;
    private Action _fadedOutCallBack;

    public void FadeIn(Action fadedInCallBack)
    {
        if (isFading)
            return;
        isFading = true;
        _fadedInCallBack = fadedInCallBack;
        animator.SetBool("Faded", true);
    }

    public void FadeOut(Action fadedOutCallBack)
    {
        if (isFading)
            return;
        isFading = true;
        _fadedOutCallBack = fadedOutCallBack;
        animator.SetBool("Faded", false);
    }

    private void Handle_FadeInAnimatorOver()
    {
        _fadedInCallBack?.Invoke();
        _fadedInCallBack = null;
        isFading = false;
    }

    private void Handle_FadeOutAnimatorOver()
    {
        _fadedOutCallBack?.Invoke();
        _fadedOutCallBack = null;
        isFading = false;
    }
}
