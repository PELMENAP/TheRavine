using System;
using UnityEngine;
using TMPro;

public class FaderOnTransit : MonoBehaviour
{
    private const string Fader_path = "Objects/Fader";

    [SerializeField] private Animator animator;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Camera _camera;
    private static FaderOnTransit _instance;

    public static FaderOnTransit instance
    {
        get
        {
            if (_instance == null)
            {
                var prefab = Resources.Load<FaderOnTransit>(Fader_path);
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
        if (isFading) return;
        isFading = true;
        _fadedInCallBack = fadedInCallBack;
        animator.SetBool("Faded", true);
    }

    public void FadeOut(Action fadedOutCallBack)
    {
        if (isFading) return;
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
        Destroy(FaderOnTransit.instance.gameObject);
    }

    public void SetLogs(string text)
    {
        loadingText.text = text;
    }

    public Camera GetFaderCamera() => _camera;
}
