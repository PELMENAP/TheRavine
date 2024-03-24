using Cysharp.Threading.Tasks;

using TheRavine.Base;

public class SceneTransitor
{
    private bool _isLoading = false;

    public async UniTaskVoid LoadScene(int numberSceneToTranslate)
    {
        if (_isLoading)
        {
            return;
        }
        
        Settings.SceneNumber = numberSceneToTranslate;
        _isLoading = true;
        bool waitFading = true;

        FaderOnTransit.instance.FadeIn(() => waitFading = false);

        await UniTask.WaitUntil(() => waitFading == false);

        UnityEngine.SceneManagement.SceneManager.LoadScene(numberSceneToTranslate);

        _isLoading = false;
    }
}
