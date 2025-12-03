using UnityEngine;
using UnityEngine.Rendering.Universal;

using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.Extensions;

using System;
using TMPro;

public class ParallaxSceneController : MonoBehaviour
{
    [SerializeField] private AudioRadioController controller;
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private UniversalAdditionalCameraData _cameraData;
    [SerializeField] private GameObject winObject;
    [SerializeField] private int timeToDelay;
    [SerializeField] private TimerType timerType;
    [SerializeField] private TextMeshPro textMeshPro;
    // [SerializeField] private bool win;
    private SyncedTimer _timer;
    private SceneLoader transistor;

    public void AddCameraToStack(Camera _cameraToAdd) => _cameraData.cameraStack.Add(_cameraToAdd);
    private WorldRegistry worldManager;
    private WorldInfo worldinfo;
    private async void Awake()
    {
        worldManager = ServiceLocator.GetService<WorldRegistry>();
        worldinfo = await worldManager.GetWorldInfoAsync(worldManager.CurrentWorldName);
        // DataStorage.winTheGame = win;
        AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());
        transistor = new SceneLoader();

        _timer = new SyncedTimer(timerType, worldinfo.IsGameWon ? timeToDelay * 6 : timeToDelay);
        _timer.TimerFinished += TimerFinished;

        _timer.Start();

        FaderOnTransit.Instance.FadeOut(null);

        if (worldinfo.IsGameWon)
        {
            winObject.SetActive(true);
            controller.StartWinRadio(audioClip);
            PrintText($"Игра пройдена за {Convert.ToString(DateTimeOffset.Now - worldinfo.CreatedTime)} секунд \r\nЗа {worldinfo.CycleCount} останов{Extension.GetSklonenie(worldinfo.CycleCount)}").Forget();
        }
        else controller.StartDefaultRadio();
    }

    private void TimerFinished()
    {
        if(worldinfo.IsGameWon) transistor.LoadScene(0).Forget();
        else transistor.LoadScene(2).Forget();
        AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());
    }

    private async UniTaskVoid PrintText(string text)
    {
        textMeshPro.text = "";
        for(int i = 0; i < text.Length; i++)
        {
            textMeshPro.text += text[i];
            await UniTask.Delay(100);
        }
    }

}
