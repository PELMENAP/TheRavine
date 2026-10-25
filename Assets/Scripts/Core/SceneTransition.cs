using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public class SceneLoader
{
    private bool _isLoading = false;

    public async UniTask LoadScene(int numberSceneToTranslate, Action beforeLoad = null)
    {
        if (_isLoading) return;

        _isLoading = true;
        bool waitFading = true;

        FaderOnTransit.Instance.FadeIn(() => waitFading = false);

        await UniTask.WaitUntil(() => waitFading == false);

        beforeLoad?.Invoke();

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