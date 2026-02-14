using UnityEngine;
using UnityEngine.Rendering.Universal;

using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.Extensions;

using System;
using TMPro;

public class ParallaxSceneController : MonoBehaviour
{
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
    private WorldRegistry worldRegistry;
    private WorldState worldData;
    private void Awake()
    {
        worldRegistry = ServiceLocator.GetService<WorldRegistry>();
        worldData = worldRegistry.GetCurrentState();
        // DataStorage.winTheGame = win;
        AddCameraToStack(FaderOnTransit.Instance.GetFaderCamera());
        transistor = new SceneLoader();

        _timer = new SyncedTimer(timerType, worldData.gameWon ? timeToDelay * 6 : timeToDelay);
        _timer.TimerFinished += TimerFinished;

        _timer.Start();

        FaderOnTransit.Instance.FadeOut(null);

        if (worldData.gameWon)
        {
            winObject.SetActive(true);
            PrintText($"Игра пройдена за {Convert.ToString(DateTimeOffset.Now.ToUnixTimeSeconds() - worldData.startTime)} секунд \r\nЗа {worldData.cycleCount} останов{Extension.GetSklonenie(worldData.cycleCount)}").Forget();
        }
    }

    private void TimerFinished()
    {
        if(worldData.gameWon) transistor.LoadScene(0).Forget();
        
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
