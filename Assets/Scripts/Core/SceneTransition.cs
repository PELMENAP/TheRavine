using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader
{
    private const float FadeTimeoutSeconds = 4f;
    private bool _isLoading = false;

    public async UniTask LoadScene(int numberSceneToTranslate, Action beforeLoad = null)
    {
        if (_isLoading) return;

        _isLoading = true;
        bool waitFading = true;

        float fadeDeadline = Time.unscaledTime + FadeTimeoutSeconds;
        try
        {
            FaderOnTransit.Instance.FadeIn(() => waitFading = false);
        }
        catch (Exception ex)
        {
            waitFading = false;
            Debug.LogError($"SceneLoader: fade failed: {ex}");
        }

        await UniTask.WaitUntil(() => waitFading == false || Time.unscaledTime >= fadeDeadline);

        try
        {
            beforeLoad?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"SceneLoader: beforeLoad failed: {ex}");
        }

        var loadScene = SceneManager.LoadSceneAsync(numberSceneToTranslate);
        loadScene.allowSceneActivation = false; 

        while (!loadScene.isDone)
        {
            if (loadScene.progress >= 0.9f)
            {
                loadScene.allowSceneActivation = true;
            }

            await UniTask.Yield();
        }

        _isLoading = false;
    }
}