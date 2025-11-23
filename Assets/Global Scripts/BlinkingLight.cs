using UnityEngine;

using Cysharp.Threading.Tasks;

public class BlinkingLight : MonoBehaviour
{
    [SerializeField] private UnityEngine.Rendering.Universal.Light2D light2D;
    [SerializeField] private float minIntensity = 0f;
    [SerializeField] private float maxIntensity = 1f;
    [SerializeField] private float duration = 1f;

    private void Start()
    {
        BlinkLight().Forget();
    }

    private async UniTaskVoid BlinkLight()
    {
        while (true)
        {
            await LerpIntensity(minIntensity, maxIntensity, duration / 2);
            await LerpIntensity(maxIntensity, minIntensity, duration / 2);
        }
    }

    private async UniTask LerpIntensity(float start, float end, float time)
    {
        float elapsedTime = 0f;
        while (elapsedTime < time)
        {
            light2D.intensity = Mathf.Lerp(start, end, elapsedTime / time);
            elapsedTime += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        light2D.intensity = end;
    }
}
