using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LitMotion;
using LitMotion.Extensions;

public class FaderOnTransit : MonoBehaviour
{
    private const string Fader_path = "Objects/Fader";

    [SerializeField] private RawImage fadeImage;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Camera _camera;
    
    private static FaderOnTransit _instance;
    private MotionHandle _currentMotionHandle;

    public static FaderOnTransit Instance
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

    public bool IsFading { get; private set; }

    private Action _fadedInCallBack;
    private Action _fadedOutCallBack;

    private void Awake()
    {
        if (fadeImage != null)
        {
            var color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;
            fadeImage.raycastTarget = false;
        }
    }

    private void OnDestroy()
    {
        if (_currentMotionHandle.IsActive())
        {
            _currentMotionHandle.Cancel();
        }
    }

    public void FadeIn(Action fadedInCallBack)
    {
        if (IsFading) return;
        
        IsFading = true;
        _fadedInCallBack = fadedInCallBack;
        
        if (_currentMotionHandle.IsActive())
        {
            _currentMotionHandle.Cancel();
        }

        _currentMotionHandle = LMotion.Create(fadeImage.color.a, 1f, 2f)
            .WithOnComplete(() => Handle_FadeInComplete())
            .BindToColorA(fadeImage);
    }

    public void FadeOut(Action fadedOutCallBack)
    {
        if (IsFading) return;
        
        var color = fadeImage.color;
        color.a = 1f;
        fadeImage.color = color;


        IsFading = true;
        _fadedOutCallBack = fadedOutCallBack;
        
        if (_currentMotionHandle.IsActive())
        {
            _currentMotionHandle.Cancel();
        }

        _currentMotionHandle = LMotion.Create(fadeImage.color.a, 0f, 2f)
            .WithOnComplete(() => Handle_FadeOutComplete())
            .BindToColorA(fadeImage);
    }

    private void Handle_FadeInComplete()
    {
        _fadedInCallBack?.Invoke();
        _fadedInCallBack = null;
        IsFading = false;
    }

    private void Handle_FadeOutComplete()
    {
        _fadedOutCallBack?.Invoke();
        _fadedOutCallBack = null;
        IsFading = false;
        Destroy(gameObject);
    }

    public void SetLogs(string text)
    {
        if (loadingText != null)
            loadingText.text = text;
    }

    public Camera GetFaderCamera() => _camera;
}