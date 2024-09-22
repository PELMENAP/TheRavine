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
    private SceneTransistor transistor;

    public void AddCameraToStack(Camera _cameraToAdd) => _cameraData.cameraStack.Add(_cameraToAdd);

    private void Awake() 
    {
        // DataStorage.winTheGame = win;
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        transistor = new SceneTransistor();

        _timer = new SyncedTimer(timerType, DataStorage.winTheGame ? timeToDelay * 6 : timeToDelay);
        _timer.TimerFinished += TimerFinished;

        _timer.Start();

        FaderOnTransit.instance.FadeOut(null);

        if(DataStorage.winTheGame) 
        {
            winObject.SetActive(true);
            controller.StartWinRadio(audioClip);
            PrintText($"Игра пройдена за {Convert.ToString(Time.time - DataStorage.startTime)} секунд \r\nЗа {DataStorage.cycleCount} останов{Extension.GetSklonenie(DataStorage.cycleCount)}").Forget();
        }
        else controller.StartDefaultRadio();
    }

    private void TimerFinished()
    {
        if(DataStorage.winTheGame) transistor.LoadScene(0).Forget();
        else transistor.LoadScene(2).Forget();
        Settings.isLoad = false;
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
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
