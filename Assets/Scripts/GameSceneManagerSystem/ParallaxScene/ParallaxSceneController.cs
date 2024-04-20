using UnityEngine;
using UnityEngine.Rendering.Universal;

using TheRavine.Base;

public class ParallaxSceneController : MonoBehaviour
{
    [SerializeField] private UniversalAdditionalCameraData _cameraData;
    [SerializeField] private int timeToDelay;
    [SerializeField] private TimerType timerType;
    private SyncedTimer _timer;
    private SceneTransitor trasitor;

    public void AddCameraToStack(Camera _cameraToAdd) => _cameraData.cameraStack.Add(_cameraToAdd);

    private void Awake() {
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
        trasitor = new SceneTransitor();

        _timer = new SyncedTimer(timerType, timeToDelay);
        _timer.TimerFinished += TimerFinished;

        _timer.Start();

        FaderOnTransit.instance.FadeOut(null);
    }

    private void TimerFinished()
    {
        trasitor.LoadScene(2).Forget();
        Settings.isLoad = false;
        AddCameraToStack(FaderOnTransit.instance.GetFaderCamera());
    }
}
